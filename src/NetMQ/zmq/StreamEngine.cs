/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.Net.Sockets;
using AsyncIO;

namespace NetMQ.zmq
{
    public class StreamEngine : IEngine, IProcatorEvents, IMsgSink
    {
        //  Size of the greeting message:
        //  Preamble (10 bytes) + version (1 byte) + socket type (1 byte).
        private const int GreetingSize = 12;

        //  Position of the version field in the greeting.
        private const int VersionPos = 10;

        //private IOObject io_object;
        private AsyncSocket m_handle;

        private ByteArraySegment m_inpos;
        private int m_insize;
        private DecoderBase m_decoder;
        private bool m_ioEnabled;

        private ByteArraySegment m_outpos;
        private int m_outsize;
        private EncoderBase m_encoder;

        //  When true, we are still trying to determine whether
        //  the peer is using versioned protocol, and if so, which
        //  version.  When false, normal message flow has started.
        private bool m_handshaking;

        //const int greeting_size = 12;

        //  The receive buffer holding the greeting message
        //  that we are receiving from the peer.
        private readonly byte[] m_greeting = new byte[12];

        //  The number of bytes of the greeting message that
        //  we have already received.

        private int m_greetingBytesRead;

        //  The send buffer holding the greeting message
        //  that we are sending to the peer.
        private readonly ByteArraySegment m_greetingOutputBuffer = new byte[12];

        //  The session this engine is attached to.
        private SessionBase m_session;

        //  Detached transient session.
        //private SessionBase leftover_session;

        private readonly Options m_options;

        // String representation of endpoint
        private readonly String m_endpoint;

        private bool m_plugged;

        // Socket
        private SocketBase m_socket;

        private IOObject m_ioObject;

        private SendState m_sending;
        private ReceiveState m_receiving;

        enum ReceiveState
        {
            Idle, Active, Stuck,
        }

        enum SendState
        {
            Idle, Active
        }


        public StreamEngine(AsyncSocket fd, Options options, String endpoint)
        {
            m_handle = fd;
            m_insize = 0;
            m_ioEnabled = false;
            m_sending = SendState.Idle;
            m_receiving = ReceiveState.Idle;
            m_outsize = 0;
            m_handshaking = true;
            m_session = null;
            m_options = options;
            m_plugged = false;
            m_endpoint = endpoint;
            m_socket = null;
            m_encoder = null;
            m_decoder = null;

            //  Set the socket buffer limits for the underlying socket.
            if (m_options.SendBuffer != 0)
            {
                m_handle.SendBufferSize = m_options.SendBuffer;
            }
            if (m_options.ReceiveBuffer != 0)
            {
                m_handle.ReceiveBufferSize = m_options.ReceiveBuffer;
            }
        }

        public void Destroy()
        {
            Debug.Assert(!m_plugged);

            if (m_handle != null)
            {
                try
                {
                    m_handle.Dispose();
                }
                catch (SocketException)
                {
                }
                m_handle = null;
            }
        }

        public void Plug(IOThread ioThread,
                         SessionBase session)
        {
            Debug.Assert(!m_plugged);
            m_plugged = true;

            //  Connect to session object.
            Debug.Assert(m_session == null);
            Debug.Assert(session != null);
            m_session = session;
            m_socket = m_session.Socket;

            m_ioObject = new IOObject(null);
            m_ioObject.SetHandler(this);

            //  Connect to I/O threads poller object.
            m_ioObject.Plug(ioThread);
            m_ioObject.AddSocket(m_handle);
            m_ioEnabled = true;

            if (m_options.RawSocket)
            {
                m_encoder = new RawEncoder(Config.OutBatchSize, session, m_options.Endian);
                m_decoder = new RawDecoder(Config.InBatchSize, m_options.Maxmsgsize, session, m_options.Endian);
                m_handshaking = false;
            }
            else
            {
                //  Send the 'length' and 'flags' fields of the identity message.
                //  The 'length' field is encoded in the long format.

                m_greetingOutputBuffer[m_outsize++] = ((byte)0xff);
                m_greetingOutputBuffer.PutLong(m_options.Endian, (long)m_options.IdentitySize + 1, 1);
                m_outsize += 8;
                m_greetingOutputBuffer[m_outsize++] = ((byte)0x7f);

                m_outpos = new ByteArraySegment(m_greetingOutputBuffer);
            }

            //  Flush all the data that may have been already received downstream.
            BeginReceiving();
            BeginSending();
        }

        private void Unplug()
        {
            Debug.Assert(m_plugged);
            m_plugged = false;

            //  Cancel all fd subscriptions.
            if (m_ioEnabled)
            {
                m_ioObject.RemoveSocket(m_handle);
                m_ioEnabled = false;
            }


            //  Disconnect from I/O threads poller object.
            m_ioObject.Unplug();

            //  Disconnect from session object.
            if (m_encoder != null)
                m_encoder.SetMsgSource(null);
            if (m_decoder != null)
                m_decoder.SetMsgSink(null);
            m_session = null;
        }

        public void Terminate()
        {
            Unplug();
            Destroy();
        }

        private void BeginReceiving()
        {
            if (m_handshaking)
            {                
                BeginHandshake();
            }
            else
            {
                m_receiving = ReceiveState.Active;

                //  Retrieve the buffer and read as much data as possible.
                //  Note that buffer can be arbitrarily large. However, we assume
                //  the underlying TCP layer has fixed buffer size and thus the
                //  number of bytes read will be always limited.
                m_decoder.GetBuffer(ref m_inpos, ref m_insize);
                BeginRead(m_inpos, m_insize);
            }
        }

        public void InCompleted(SocketError socketError, int bytesTransferred)
        {            
            m_receiving = ReceiveState.Idle;

            //  If still handshaking, receive and process the greeting message.
            if (m_handshaking)
            {
                Handshake(socketError, bytesTransferred);
            }
            else
            {                
                Debug.Assert(m_decoder != null);

                Console.WriteLine("{0} Receiving {1} {2}", m_socket.ThreadId, socketError, bytesTransferred);

                m_insize = EndRead(socketError, bytesTransferred);

                ProcessIn();
            }
        }

        public void ActivateIn()
        {
            if (!m_ioEnabled)
            {
                //  There was an input error but the engine could not
                //  be terminated (due to the stalled decoder).
                //  Flush the pending message and terminate the engine now.
                m_decoder.ProcessBuffer(m_inpos, 0);
                Debug.Assert(!m_decoder.Stalled());
                m_session.Flush();
                Error();
                return;
            }
            else if (m_receiving == ReceiveState.Idle)
            {
                BeginReceiving();
            }
            else if (m_receiving == ReceiveState.Stuck)
            {
                ProcessIn();
            }
        }

        private void ProcessIn()
        {
            bool disconnection = false;
            int processed;

            if (m_insize == -1)
            {
                m_insize = 0;
                disconnection = true;
            }

            if (m_options.RawSocket)
            {
                if (m_insize == 0 || !m_decoder.MessageReadySize(m_insize))
                {
                    processed = 0;
                }
                else
                {
                    processed = m_decoder.ProcessBuffer(m_inpos, m_insize);
                }
            }
            else
            {
                //  Push the data to the decoder.
                processed = m_decoder.ProcessBuffer(m_inpos, m_insize);
            }

            if (processed == -1)
            {
                disconnection = true;
            }
            else
            {
                //  Stop polling for input if we got stuck.
                if (processed < m_insize)
                {
                    m_receiving = ReceiveState.Stuck;
                }

                m_inpos.AdvanceOffset(processed);
                m_insize -= processed;
            }

            //  Flush all messages the decoder may have produced.
            m_session.Flush();

            //  An input error has occurred. If the last decoded message
            //  has already been accepted, we terminate the engine immediately.
            //  Otherwise, we stop waiting for socket events and postpone
            //  the termination until after the message is accepted.
            if (disconnection)
            {
                if (m_decoder.Stalled())
                {
                    m_ioObject.RemoveSocket(m_handle);
                    m_ioEnabled = false;
                }
                else
                    Error();
            }
            else if (m_receiving != ReceiveState.Stuck)
            {
                BeginReceiving();
            }
        }

        private void BeginSending()
        {
            if (m_outsize == 0)
            {
                if (m_encoder == null)
                {
                    Debug.Assert(m_handshaking);
                    return;
                }
                else
                {
                    m_outpos = null;
                    m_encoder.GetData(ref m_outpos, ref m_outsize);

                    if (m_outsize == 0)
                    {
                        return;
                    }
                }
            }

            //  If there are any data to write in write buffer, write as much as
            //  possible to the socket. Note that amount of data to write can be
            //  arbitratily large. However, we assume that underlying TCP layer has
            //  limited transmission buffer and thus the actual number of bytes
            //  written should be reasonably modest.
            m_sending = SendState.Active;     
       
            if (!m_handshaking)
                Console.WriteLine("{0} Sending {1}", m_socket.ThreadId, m_outsize);

            BeginWrite(m_outpos, m_outsize);
        }

        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {            
            int nbytes = EndWrite(socketError, bytesTransferred);

            m_sending = SendState.Idle;

            //  IO error has occurred. We stop waiting for output events.
            //  The engine is not terminated until we detect input error;
            //  this is necessary to prevent losing incomming messages.
            if (nbytes != -1)
            {
                m_outpos.AdvanceOffset(nbytes);
                m_outsize -= nbytes;

                //  If we are still handshaking and there are no data
                //  to send, stop polling for output.
                if (!(m_handshaking && m_outsize == 0))
                {
                    BeginSending();
                }                
            }            
        }

        public void ActivateOut()
        {
            if (m_sending == SendState.Idle)
            {
                BeginSending();    
            }            
        }

        private void BeginHandshake()
        {
            m_receiving = ReceiveState.Active;

            ByteArraySegment greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
            BeginRead(greetingSegment, GreetingSize);
        }

        private void Handshake(SocketError socketError, int bytesTransferred)
        {
            Debug.Assert(m_handshaking);

            int n = EndRead(socketError, bytesTransferred);

            if (n == -1)
            {
                Error();
                return;
            }

            m_greetingBytesRead += n;

            //  We have received at least one byte from the peer.
            //  If the first byte is not 0xff, we know that the
            //  peer is using unversioned protocol.
            if (m_greeting[0] != 0xff)
            {
                UnversionProtocol();
            }
            else
            {
                if (m_greetingBytesRead >= 10)
                {
                    //  Inspect the right-most bit of the 10th byte (which coincides
                    //  with the 'flags' field if a regular message was sent).
                    //  Zero indicates this is a header of identity message
                    //  (i.e. the peer is using the unversioned protocol).
                    if ((m_greeting[9] & 0x01) == 0)
                    {
                        UnversionProtocol();
                        return;
                    }
                    else
                    {
                        //  The peer is using versioned protocol.
                        //  Send the rest of the greeting, if necessary.
                        if (!(((byte[])m_outpos) == ((byte[])m_greetingOutputBuffer) &&
                              m_outpos.Offset + m_outsize == GreetingSize))
                        {
                            m_outpos[m_outsize++] = 1; // Protocol version
                            m_outpos[m_outsize++] = (byte)m_options.SocketType;

                            if (m_sending == SendState.Idle)
                                BeginSending();
                        }
                    }
                }

                if (m_greetingBytesRead < GreetingSize)
                {
                    m_receiving = ReceiveState.Active;

                    ByteArraySegment greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                    BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                }
                else
                {
                    if (m_greeting[VersionPos] == 0)
                    {
                        //  ZMTP/1.0 framing.
                        m_encoder = new Encoder(Config.OutBatchSize, m_options.Endian);
                        m_encoder.SetMsgSource(m_session);

                        m_decoder = new Decoder(Config.InBatchSize, m_options.Maxmsgsize, m_options.Endian);
                        m_decoder.SetMsgSink(m_session);
                    }
                    else
                    {
                        //  v1 framing protocol.
                        m_encoder = new V1Encoder(Config.OutBatchSize, m_session, m_options.Endian);

                        m_decoder = new V1Decoder(Config.InBatchSize, m_options.Maxmsgsize, m_session, m_options.Endian);
                    }

                    //  Handshaking was successful.
                    //  Switch into the normal message flow.
                    m_handshaking = false;

                    if (m_sending == SendState.Idle)
                        BeginSending();

                    BeginReceiving();
                }
            }
        }

        private void UnversionProtocol()
        {
            m_encoder = new Encoder(Config.OutBatchSize, m_options.Endian);
            m_encoder.SetMsgSource(m_session);

            m_decoder = new Decoder(Config.InBatchSize, m_options.Maxmsgsize, m_options.Endian);
            m_decoder.SetMsgSink(m_session);

            //  We have already sent the message header.
            //  Since there is no way to tell the encoder to
            //  skip the message header, we simply throw that
            //  header data away.
            int headerSize = m_options.IdentitySize + 1 >= 255 ? 10 : 2;
            byte[] tmp = new byte[10];
            ByteArraySegment bufferp = new ByteArraySegment(tmp);

            int bufferSize = headerSize;

            m_encoder.GetData(ref bufferp, ref bufferSize);

            Debug.Assert(bufferSize == headerSize);

            //  Make sure the decoder sees the data we have already received.
            m_inpos = new ByteArraySegment(m_greeting);
            m_insize = m_greetingBytesRead;

            //  To allow for interoperability with peers that do not forward
            //  their subscriptions, we inject a phony subsription
            //  message into the incomming message stream. To put this
            //  message right after the identity message, we temporarily
            //  divert the message stream from session to ourselves.
            if (m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub)
                m_decoder.SetMsgSink(this);

            //  Handshaking was successful.
            //  Switch into the normal message flow.
            m_handshaking = false;

            if (m_sending == SendState.Idle)
                BeginSending();

            if (m_insize == 0)
            {
                BeginReceiving();
            }
            else
            {
                ProcessIn();
            }
        }

        public void PushMsg(ref Msg msg)
        {
            Debug.Assert(m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub);

            //  The first message is identity.
            //  Let the session process it.

            m_session.PushMsg(ref msg);

            //  Inject the subscription message so that the ZMQ 2.x peer
            //  receives our messages.
            msg.InitPool(1);
            msg.Put((byte)1);

            m_session.PushMsg(ref msg);

            m_session.Flush();

            //  Once we have injected the subscription message, we can
            //  Divert the message flow back to the session.
            Debug.Assert(m_decoder != null);
            m_decoder.SetMsgSink(m_session);
        }

        private void Error()
        {
            Debug.Assert(m_session != null);
            m_socket.EventDisconnected(m_endpoint, m_handle);
            m_session.Detach();
            Unplug();
            Destroy();
        }

        private int EndWrite(SocketError socketError, int bytesTransferred)
        {
            if (socketError == SocketError.Success && bytesTransferred > 0)
            {
                return bytesTransferred;
            }
            if (bytesTransferred == 0 ||
                socketError == SocketError.NetworkDown ||
                socketError == SocketError.NetworkReset ||
                socketError == SocketError.HostUnreachable ||
                socketError == SocketError.ConnectionAborted ||
                socketError == SocketError.TimedOut ||
                socketError == SocketError.ConnectionReset)
            {
                return -1;
            }
            else
            {
                throw NetMQException.Create(ErrorHelper.SocketErrorToErrorCode(socketError));
            }
        }

        private void BeginWrite(ByteArraySegment data, int size)
        {
            m_handle.Send((byte[])data, data.Offset, size, SocketFlags.None);
        }

        private int EndRead(SocketError socketError, int bytesTransferred)
        {
            if (socketError == SocketError.Success && bytesTransferred > 0)
            {
                return bytesTransferred;
            }
            else if (bytesTransferred == 0 ||
                socketError == SocketError.NetworkDown ||
                socketError == SocketError.NetworkReset ||
                socketError == SocketError.HostUnreachable ||
                socketError == SocketError.ConnectionAborted ||
                socketError == SocketError.TimedOut ||
                socketError == SocketError.ConnectionReset)
            {
                return -1;
            }
            else
            {
                throw NetMQException.Create(ErrorHelper.SocketErrorToErrorCode(socketError));
            }
        }

        private void BeginRead(ByteArraySegment data, int size)
        {
            m_handle.Receive((byte[])data, data.Offset, size, SocketFlags.None);
        }

        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}