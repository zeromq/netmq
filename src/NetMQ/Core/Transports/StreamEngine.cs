/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    internal sealed class StreamEngine : IEngine, IProactorEvents, IMsgSink
    {
        private class StateMachineAction
        {
            public StateMachineAction(Action action, SocketError socketError, int bytesTransferred)
            {
                Action = action;
                SocketError = socketError;
                BytesTransferred = bytesTransferred;
            }

            public Action Action { get; }

            public SocketError SocketError { get; }

            public int BytesTransferred { get; }
        }

        /// <summary>
        /// This enum-type denotes the operational state of this StreamEngine - whether Closed, doing Handshaking, Active, or Stalled.
        /// </summary>
        private enum State
        {
            Closed,
            Handshaking,
            Active,
            Stalled,
        }

        private enum HandshakeState
        {
            Closed,
            SendingGreeting,
            ReceivingGreeting,
            SendingRestOfGreeting,
            ReceivingRestOfGreeting
        }

        private enum ReceiveState
        {
            Idle,
            Active,
            Stuck,
        }

        private enum SendState
        {
            Idle,
            Active,
            Error
        }

        private enum Action
        {
            Start,
            InCompleted,
            OutCompleted,
            ActivateOut,
            ActivateIn
        }

        // Size of the greeting message:
        // Preamble (10 bytes) + version (1 byte) + socket type (1 byte).
        private const int GreetingSize = 12;
        private const int PreambleSize = 10;

        // Position of the version field in the greeting.
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

        // The receive buffer holding the greeting message
        // that we are receiving from the peer.
        private readonly byte[] m_greeting = new byte[12];

        // The number of bytes of the greeting message that
        // we have already received.

        private int m_greetingBytesRead;

        // The send buffer holding the greeting message
        // that we are sending to the peer.
        private readonly ByteArraySegment m_greetingOutputBuffer = new byte[12];

        // The session this engine is attached to.
        private SessionBase m_session;

        // Detached transient session.
        //private SessionBase leftover_session;

        private readonly Options m_options;

        // string representation of endpoint
        private readonly string m_endpoint;

        private bool m_plugged;

        // Socket
        private SocketBase m_socket;

        private IOObject m_ioObject;

        private SendState m_sendingState;
        private ReceiveState m_receivingState;

        private State m_state;
        private HandshakeState m_handshakeState;

        // queue for actions that happen during the state machine
        private readonly Queue<StateMachineAction> m_actionsQueue;

        public StreamEngine(AsyncSocket handle, Options options, string endpoint)
        {
            m_handle = handle;
            m_insize = 0;
            m_ioEnabled = false;
            m_sendingState = SendState.Idle;
            m_receivingState = ReceiveState.Idle;
            m_outsize = 0;
            m_session = null;
            m_options = options;
            m_plugged = false;
            m_endpoint = endpoint;
            m_socket = null;
            m_encoder = null;
            m_decoder = null;
            m_actionsQueue = new Queue<StateMachineAction>();

            // Set the socket buffer limits for the underlying socket.
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
                {}
                m_handle = null;
            }
        }

        public void Plug(IOThread ioThread, SessionBase session)
        {
            Debug.Assert(!m_plugged);
            m_plugged = true;

            // Connect to session object.
            Debug.Assert(m_session == null);
            Debug.Assert(session != null);
            m_session = session;
            m_socket = m_session.Socket;

            m_ioObject = new IOObject(null);
            m_ioObject.SetHandler(this);

            // Connect to I/O threads poller object.
            m_ioObject.Plug(ioThread);
            m_ioObject.AddSocket(m_handle);
            m_ioEnabled = true;

            FeedAction(Action.Start, SocketError.Success, 0);
        }

        public void Terminate()
        {
            Unplug();
            Destroy();
        }

        private void Unplug()
        {
            Debug.Assert(m_plugged);
            m_plugged = false;

            // remove handle from proactor.
            if (m_ioEnabled)
            {
                m_ioObject.RemoveSocket(m_handle);
                m_ioEnabled = false;
            }

            // Disconnect from I/O threads poller object.
            m_ioObject.Unplug();

            m_state = State.Closed;

            // Disconnect from session object.
            m_encoder?.SetMsgSource(null);
            m_decoder?.SetMsgSink(null);
            m_session = null;
        }

        private void Error()
        {
            Debug.Assert(m_session != null);
            m_socket.EventDisconnected(m_endpoint, m_handle);
            m_session.Detach();
            Unplug();
            Destroy();
        }

        private void FeedAction(Action action, SocketError socketError, int bytesTransferred)
        {
            Handle(action, socketError, bytesTransferred);

            while (m_actionsQueue.Count > 0)
            {
                var stateMachineAction = m_actionsQueue.Dequeue();
                Handle(stateMachineAction.Action, stateMachineAction.SocketError, stateMachineAction.BytesTransferred);
            }
        }

        private void EnqueueAction(Action action, SocketError socketError, int bytesTransferred)
        {
            m_actionsQueue.Enqueue(new StateMachineAction(action, socketError, bytesTransferred));
        }

        private void Handle(Action action, SocketError socketError, int bytesTransferred)
        {
            switch (m_state)
            {
                case State.Closed:
                    switch (action)
                    {
                        case Action.Start:
                            if (m_options.RawSocket)
                            {
                                m_encoder = new RawEncoder(Config.OutBatchSize, m_session, m_options.Endian);
                                m_decoder = new RawDecoder(Config.InBatchSize, m_options.MaxMessageSize, m_session, m_options.Endian);

                                Activate();
                            }
                            else
                            {
                                m_state = State.Handshaking;
                                m_handshakeState = HandshakeState.Closed;
                                HandleHandshake(action, socketError, bytesTransferred);
                            }

                            break;
                    }
                    break;
                case State.Handshaking:
                    HandleHandshake(action, socketError, bytesTransferred);
                    break;
                case State.Active:
                    switch (action)
                    {
                        case Action.InCompleted:
                            m_insize = EndRead(socketError, bytesTransferred);

                            ProcessInput();
                            break;
                        case Action.ActivateIn:

                            // if we stuck let's continue, other than that nothing to do
                            if (m_receivingState == ReceiveState.Stuck)
                            {
                                m_receivingState = ReceiveState.Active;
                                ProcessInput();
                            }
                            break;
                        case Action.OutCompleted:
                            int bytesSent = EndWrite(socketError, bytesTransferred);

                            // IO error has occurred. We stop waiting for output events.
                            // The engine is not terminated until we detect input error;
                            // this is necessary to prevent losing incoming messages.
                            if (bytesSent == -1)
                            {
                                m_sendingState = SendState.Error;
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                BeginSending();
                            }
                            break;
                        case Action.ActivateOut:
                            // if we idle we start sending, other than do nothing
                            if (m_sendingState == SendState.Idle)
                            {
                                m_sendingState = SendState.Active;
                                BeginSending();
                            }
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case State.Stalled:
                    switch (action)
                    {
                        case Action.ActivateIn:
                            // There was an input error but the engine could not
                            // be terminated (due to the stalled decoder).
                            // Flush the pending message and terminate the engine now.
                            m_decoder.ProcessBuffer(m_inpos, 0);
                            Debug.Assert(!m_decoder.Stalled());
                            m_session.Flush();
                            Error();
                            break;
                        case Action.ActivateOut:
                            break;
                    }
                    break;
            }
        }

        private void BeginSending()
        {
            if (m_outsize == 0)
            {
                m_outpos = null;
                m_encoder.GetData(ref m_outpos, ref m_outsize);

                if (m_outsize == 0)
                {
                    m_sendingState = SendState.Idle;
                }
                else
                {
                    BeginWrite(m_outpos, m_outsize);
                }
            }
            else
            {
                BeginWrite(m_outpos, m_outsize);
            }
        }

        private void HandleHandshake(Action action, SocketError socketError, int bytesTransferred)
        {
            int bytesSent;
            int bytesReceived;

            switch (m_handshakeState)
            {
                case HandshakeState.Closed:
                    switch (action)
                    {
                        case Action.Start:
                            // Send the 'length' and 'flags' fields of the identity message.
                            // The 'length' field is encoded in the long format.

                            m_greetingOutputBuffer[m_outsize++] = 0xff;
                            m_greetingOutputBuffer.PutLong(m_options.Endian, (long)m_options.IdentitySize + 1, 1);
                            m_outsize += 8;
                            m_greetingOutputBuffer[m_outsize++] = 0x7f;

                            m_outpos = new ByteArraySegment(m_greetingOutputBuffer);

                            m_handshakeState = HandshakeState.SendingGreeting;

                            BeginWrite(m_outpos, m_outsize);
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.SendingGreeting:
                    switch (action)
                    {
                        case Action.OutCompleted:
                            bytesSent = EndWrite(socketError, bytesTransferred);

                            if (bytesSent == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                if (m_outsize > 0)
                                {
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    m_greetingBytesRead = 0;

                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);

                                    m_handshakeState = HandshakeState.ReceivingGreeting;

                                    BeginRead(greetingSegment, PreambleSize);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.ReceivingGreeting:
                    switch (action)
                    {
                        case Action.InCompleted:
                            bytesReceived = EndRead(socketError, bytesTransferred);

                            if (bytesReceived == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_greetingBytesRead += bytesReceived;

                                // check if it is an unversioned protocol
                                if (m_greeting[0] != 0xff || (m_greetingBytesRead == 10 && (m_greeting[9] & 0x01) == 0))
                                {
                                    m_encoder = new V1Encoder(Config.OutBatchSize, m_options.Endian);
                                    m_encoder.SetMsgSource(m_session);

                                    m_decoder = new V1Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);
                                    m_decoder.SetMsgSink(m_session);

                                    // We have already sent the message header.
                                    // Since there is no way to tell the encoder to
                                    // skip the message header, we simply throw that
                                    // header data away.
                                    int headerSize = m_options.IdentitySize + 1 >= 255 ? 10 : 2;
                                    var tmp = new byte[10];
                                    var bufferp = new ByteArraySegment(tmp);

                                    int bufferSize = headerSize;

                                    m_encoder.GetData(ref bufferp, ref bufferSize);

                                    Debug.Assert(bufferSize == headerSize);

                                    // Make sure the decoder sees the data we have already received.
                                    m_inpos = new ByteArraySegment(m_greeting);
                                    m_insize = m_greetingBytesRead;

                                    // To allow for interoperability with peers that do not forward
                                    // their subscriptions, we inject a phony subscription
                                    // message into the incoming message stream. To put this
                                    // message right after the identity message, we temporarily
                                    // divert the message stream from session to ourselves.
                                    if (m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub)
                                        m_decoder.SetMsgSink(this);

                                    // handshake is done
                                    Activate();
                                }
                                else if (m_greetingBytesRead < 10)
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, PreambleSize - m_greetingBytesRead);
                                }
                                else
                                {
                                    // The peer is using versioned protocol.
                                    // Send the rest of the greeting.
                                    m_outpos[m_outsize++] = 1; // Protocol version
                                    m_outpos[m_outsize++] = (byte)m_options.SocketType;

                                    m_handshakeState = HandshakeState.SendingRestOfGreeting;

                                    BeginWrite(m_outpos, m_outsize);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.SendingRestOfGreeting:
                    switch (action)
                    {
                        case Action.OutCompleted:
                            bytesSent = EndWrite(socketError, bytesTransferred);

                            if (bytesSent == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                if (m_outsize > 0)
                                {
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);

                                    m_handshakeState = HandshakeState.ReceivingRestOfGreeting;
                                    BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.ReceivingRestOfGreeting:
                    switch (action)
                    {
                        case Action.InCompleted:
                            bytesReceived = EndRead(socketError, bytesTransferred);

                            if (bytesReceived == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_greetingBytesRead += bytesReceived;

                                if (m_greetingBytesRead < GreetingSize)
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                                }
                                else
                                {
                                    if (m_greeting[VersionPos] == 0)
                                    {
                                        // ZMTP/1.0 framing.
                                        m_encoder = new V1Encoder(Config.OutBatchSize, m_options.Endian);
                                        m_encoder.SetMsgSource(m_session);

                                        m_decoder = new V1Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);
                                        m_decoder.SetMsgSink(m_session);
                                    }
                                    else
                                    {
                                        // v1 framing protocol.
                                        m_encoder = new V2Encoder(Config.OutBatchSize, m_session, m_options.Endian);
                                        m_decoder = new V2Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_session, m_options.Endian);
                                    }

                                    // handshake is done
                                    Activate();
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private void Activate()
        {
            // Handshaking was successful.
            // Switch into the normal message flow.
            m_state = State.Active;

            m_outsize = 0;

            m_sendingState = SendState.Active;
            BeginSending();

            m_receivingState = ReceiveState.Active;

            if (m_insize == 0)
            {
                m_decoder.GetBuffer(out m_inpos, out m_insize);
                BeginRead(m_inpos, m_insize);
            }
            else
            {
                ProcessInput();
            }
        }

        private void ProcessInput()
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
                // Push the data to the decoder.
                processed = m_decoder.ProcessBuffer(m_inpos, m_insize);
            }

            if (processed == -1)
            {
                disconnection = true;
            }
            else
            {
                // Stop polling for input if we got stuck.
                if (processed < m_insize)
                {
                    m_receivingState = ReceiveState.Stuck;

                    m_inpos.AdvanceOffset(processed);
                    m_insize -= processed;
                }
                else
                {
                    m_inpos = null;
                    m_insize = 0;
                }
            }

            // Flush all messages the decoder may have produced.
            m_session.Flush();

            // An input error has occurred. If the last decoded message
            // has already been accepted, we terminate the engine immediately.
            // Otherwise, we stop waiting for socket events and postpone
            // the termination until after the message is accepted.
            if (disconnection)
            {
                if (m_decoder.Stalled())
                {
                    m_ioObject.RemoveSocket(m_handle);
                    m_ioEnabled = false;
                    m_state = State.Stalled;
                }
                else
                {
                    Error();
                }
            }
            else if (m_receivingState != ReceiveState.Stuck)
            {
                m_decoder.GetBuffer(out m_inpos, out m_insize);
                BeginRead(m_inpos, m_insize);
            }
        }

        /// <summary>
        /// This method is be called when a message receive operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            FeedAction(Action.InCompleted, socketError, bytesTransferred);
        }

        public void ActivateIn()
        {
            FeedAction(Action.ActivateIn, SocketError.Success, 0);
        }

        /// <summary>
        /// This method is called when a message Send operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            FeedAction(Action.OutCompleted, socketError, bytesTransferred);
        }

        public void ActivateOut()
        {
            FeedAction(Action.ActivateOut, SocketError.Success, 0);
        }

        public bool PushMsg(ref Msg msg)
        {
            Debug.Assert(m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub);

            // The first message is identity.
            // Let the session process it.

            m_session.PushMsg(ref msg);

            // Inject the subscription message so that the ZMQ 2.x peer
            // receives our messages.
            msg.InitPool(1);
            msg.Put((byte)1);

            bool isMessagePushed = m_session.PushMsg(ref msg);

            m_session.Flush();

            // Once we have injected the subscription message, we can
            // Divert the message flow back to the session.
            Debug.Assert(m_decoder != null);
            m_decoder.SetMsgSink(m_session);

            return isMessagePushed;
        }

        /// <param name="socketError">the SocketError that resulted from the write - which could be Success (no error at all)</param>
        /// <param name="bytesTransferred">this indicates the number of bytes that were transferred in the write</param>
        /// <returns>the number of bytes transferred if successful, -1 otherwise</returns>
        /// <exception cref="NetMQException">If the socketError is not Success then it must be a valid recoverable error or the number of bytes transferred must be zero.</exception>
        /// <remarks>
        /// If socketError is SocketError.Success and bytesTransferred is > 0, then this returns bytesTransferred.
        /// If bytes is zero, or the socketError is one of NetworkDown, NetworkReset, HostUn, Connection Aborted, TimedOut, or ConnectionReset, - then -1 is returned.
        /// Otherwise, a NetMQException is thrown.
        /// </remarks>
        private static int EndWrite(SocketError socketError, int bytesTransferred)
        {
            if (socketError == SocketError.Success && bytesTransferred > 0)
                return bytesTransferred;

            if (bytesTransferred == 0 ||
                socketError == SocketError.NetworkDown ||
                socketError == SocketError.NetworkReset ||
                socketError == SocketError.HostUnreachable ||
                socketError == SocketError.ConnectionAborted ||
                socketError == SocketError.TimedOut ||
                socketError == SocketError.ConnectionReset ||
                socketError == SocketError.AccessDenied)
                return -1;

            throw NetMQException.Create(socketError);
        }

        private void BeginWrite([NotNull] ByteArraySegment data, int size)
        {
            try
            {
                m_handle.Send((byte[])data, data.Offset, size, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                EnqueueAction(Action.OutCompleted, ex.SocketErrorCode, 0);
            }
        }

        /// <param name="socketError">the SocketError that resulted from the read - which could be Success (no error at all)</param>
        /// <param name="bytesTransferred">this indicates the number of bytes that were transferred in the read</param>
        /// <returns>the number of bytes transferred if successful, -1 otherwise</returns>
        /// <exception cref="NetMQException">If the socketError is not Success then it must be a valid recoverable error or the number of bytes transferred must be zero.</exception>
        /// <remarks>
        /// If socketError is SocketError.Success and bytesTransferred is > 0, then this returns bytesTransferred.
        /// If bytes is zero, or the socketError is one of NetworkDown, NetworkReset, HostUn, Connection Aborted, TimedOut, or ConnectionReset, - then -1 is returned.
        /// Otherwise, a NetMQException is thrown.
        /// </remarks>
        private static int EndRead(SocketError socketError, int bytesTransferred)
        {
            if (socketError == SocketError.Success && bytesTransferred > 0)
                return bytesTransferred;

            if (bytesTransferred == 0 ||
                socketError == SocketError.NetworkDown ||
                socketError == SocketError.NetworkReset ||
                socketError == SocketError.HostUnreachable ||
                socketError == SocketError.ConnectionAborted ||
                socketError == SocketError.TimedOut ||
                socketError == SocketError.ConnectionReset ||
                socketError == SocketError.AccessDenied)
                return -1;

            throw NetMQException.Create(socketError);
        }

        private void BeginRead([NotNull] ByteArraySegment data, int size)
        {
            try
            {
                m_handle.Receive((byte[])data, data.Offset, size, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                EnqueueAction(Action.InCompleted, ex.SocketErrorCode, 0);
            }
        }

        /// <summary>
        /// This would be called when a timer expires, although here it only throws NotSupportedException.
        /// </summary>
        /// <param name="id">an integer used to identify the timer (not used here)</param>
        /// <exception cref="NotSupportedException">TimerEvent is not supported on StreamEngine.</exception>
        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}