using System.Diagnostics;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Core.Transports.Pgm
{
    internal sealed class PgmSession : IEngine, IProactorEvents
    {
        private AsyncSocket m_handle;
        private readonly Options m_options;
        private IOObject m_ioObject;
        private SessionBase m_session;
        private V1Decoder m_decoder;
        private bool m_joined;

        private int m_pendingBytes;
        private ByteArraySegment m_pendingData;

        private readonly ByteArraySegment m_data;

        /// <summary>
        /// This enum-type is Idle, Receiving, Stuck, or Error.
        /// </summary>
        private enum State
        {
            Idle,
            Receiving,
            Stuck,
            Error
        }

        private State m_state;

        public PgmSession([NotNull] PgmSocket pgmSocket, [NotNull] Options options)
        {
            m_handle = pgmSocket.Handle;
            m_options = options;
            m_data = new byte[Config.PgmMaxTPDU];
            m_joined = false;

            m_state = State.Idle;
        }

        void IEngine.Plug(IOThread ioThread, SessionBase session)
        {
            m_session = session;
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
        {}

        public void BeginReceive()
        {
            m_data.Reset();
            m_handle.Receive((byte[])m_data);
        }

        public void ActivateIn()
        {
            if (m_state == State.Stuck)
            {
                Debug.Assert(m_decoder != null);
                Debug.Assert(m_pendingData != null);

                // Ask the decoder to process remaining data.
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

        /// <summary>
        /// This method is be called when a message receive operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success || bytesTransferred == 0)
            {
                m_joined = false;
                Error();
            }
            else
            {
                // Read the offset of the fist message in the current packet.
                Debug.Assert(bytesTransferred >= sizeof(ushort));

                ushort offset = m_data.GetUnsignedShort(m_options.Endian, 0);
                m_data.AdvanceOffset(sizeof(ushort));
                bytesTransferred -= sizeof(ushort);

                // Join the stream if needed.
                if (!m_joined)
                {
                    // There is no beginning of the message in current packet.
                    // Ignore the data.
                    if (offset == 0xffff)
                    {
                        BeginReceive();
                        return;
                    }

                    Debug.Assert(offset <= bytesTransferred);
                    Debug.Assert(m_decoder == null);

                    // We have to move data to the beginning of the first message.
                    m_data.AdvanceOffset(offset);
                    bytesTransferred -= offset;

                    // Mark the stream as joined.
                    m_joined = true;

                    // Create and connect decoder for the peer.
                    m_decoder = new V1Decoder(0, m_options.MaxMessageSize, m_options.Endian);
                    m_decoder.SetMsgSink(m_session);
                }

                // Push all the data to the decoder.
                int processed = m_decoder.ProcessBuffer(m_data, bytesTransferred);
                if (processed < bytesTransferred)
                {
                    // Save some state so we can resume the decoding process later.
                    m_pendingBytes = bytesTransferred - processed;
                    m_pendingData = new ByteArraySegment(m_data, processed);

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

            // Disconnect from I/O threads poller object.
            m_ioObject.Unplug();

            // Disconnect from session object.
            m_decoder?.SetMsgSink(null);

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
                {}
                m_handle = null;
            }
        }

        /// <summary>
        /// This method would be called when a message Send operation has been completed, except in this case this method does nothing.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {}

        /// <summary>
        /// This would be called when a timer expires, although here it does nothing.
        /// </summary>
        /// <param name="id">an integer used to identify the timer (not used here)</param>
        public void TimerEvent(int id)
        {}

        private void DropSubscriptions()
        {
            var msg = new Msg();
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