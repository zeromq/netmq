using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using AsyncIO;

namespace NetMQ.zmq.Transports.PGM
{
    class PgmSession : IEngine, IProcatorEvents
    {
        private AsyncSocket m_handle;
        private readonly PgmSocket m_pgmSocket;
        private readonly Options m_options;
        private IOObject m_ioObject;
        private SessionBase m_session;
        private SocketBase m_socket;
        private V1Decoder m_decoder;
        private bool m_joined;

        private int m_pendingBytes;
        private ByteArraySegment m_pendingData;

        private readonly ByteArraySegment data;

        enum State
        {
            Idle,
            Receiving,
            Stuck,
            Error
        }

        private State m_state;

        public PgmSession(PgmSocket pgmSocket, Options options)
        {
            m_handle = pgmSocket.Handle;
            m_pgmSocket = pgmSocket;
            m_options = options;
            data = new byte[Config.PgmMaxTPDU];
            m_joined = false;

            m_state = State.Idle;
        }

        public void Plug(IOThread ioThread, SessionBase session)
        {
            m_session = session;

            m_socket = session.Socket;

            m_ioObject = new IOObject(null);
            m_ioObject.SetHandler(this);
            m_ioObject.Plug(ioThread);
            m_ioObject.AddSocket(m_handle);

            DropSubscriptions();

            var msg = new Msg();
            msg.InitEmpty();

            // push message to the session because there is no identity message with pgm
            session.PushMsg(ref msg);

            m_state = State.Receiving;
            BeginReceive();
        }

        public void Terminate()
        {
        }

        public void BeginReceive()
        {
            data.Reset();
            m_handle.Receive((byte[])data);
        }

        public void ActivateIn()
        {
            if (m_state == State.Stuck)
            {
                Debug.Assert(m_decoder != null);
                Debug.Assert(m_pendingData != null);

                //  Ask the decoder to process remaining data.
                int n = m_decoder.ProcessBuffer(m_pendingData, m_pendingBytes);
                m_pendingBytes -= n;
                m_session.Flush();

                if (m_pendingBytes == 0)
                {
                    m_state = State.Receiving;
                    BeginReceive();
                }
            }
        }

        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success || bytesTransferred == 0)
            {
                m_joined = false;
                Error();
            }
            else
            {
                //  Read the offset of the fist message in the current packet.
                Debug.Assert(bytesTransferred >= sizeof(ushort));

                ushort offset = data.GetUnsignedShort(m_options.Endian, 0);
                data.AdvanceOffset(sizeof(ushort));
                bytesTransferred -= sizeof(ushort);

                //  Join the stream if needed.
                if (!m_joined)
                {
                    //  There is no beginning of the message in current packet.
                    //  Ignore the data.
                    if (offset == 0xffff)
                    {
                        BeginReceive();
                        return;
                    }

                    Debug.Assert(offset <= bytesTransferred);
                    Debug.Assert(m_decoder == null);

                    //  We have to move data to the begining of the first message.
                    data.AdvanceOffset(offset);
                    bytesTransferred -= offset;

                    //  Mark the stream as joined.
                    m_joined = true;

                    //  Create and connect decoder for the peer.
                    m_decoder = new V1Decoder(0, m_options.Maxmsgsize, m_options.Endian);
                    m_decoder.SetMsgSink(m_session);
                }

                //  Push all the data to the decoder.
                int processed = m_decoder.ProcessBuffer(data, bytesTransferred);
                if (processed < bytesTransferred)
                {
                    //  Save some state so we can resume the decoding process later.
                    m_pendingBytes = bytesTransferred - processed;
                    m_pendingData = new ByteArraySegment(data, processed);

                    m_state = State.Stuck;
                }
                else
                {
                    m_session.Flush();

                    BeginReceive();
                }
            }
        }

        private void Error()
        {
            Debug.Assert(m_session != null);
                        
            m_session.Detach();
            
            m_ioObject.RemoveSocket(m_handle);

            //  Disconnect from I/O threads poller object.
            m_ioObject.Unplug();

            //  Disconnect from session object.
            if (m_decoder != null)
                m_decoder.SetMsgSink(null);

            m_session = null;

            m_state = State.Error;

            Destroy();
        }

        public void Destroy()
        {
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

        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
        }

        public void TimerEvent(int id)
        {
        }

        private void DropSubscriptions()
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            while (m_session.PullMsg(ref msg))
            {
                msg.Close();
            }
        }

        public void ActivateOut()
        {
            DropSubscriptions();
        }
    }
}
