using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Core.Transports.Pgm
{
    internal sealed class PgmSender : IOObject, IEngine, IProactorEvents
    {
        /// <summary>
        /// ID of the timer used to delay the reconnection. Value is 1.
        /// </summary>
        private const int ReconnectTimerId = 1;

        private readonly Options m_options;
        private readonly Address m_addr;
        private readonly bool m_delayedStart;
        private readonly V1Encoder m_encoder;

        private AsyncSocket m_socket;
        private PgmSocket m_pgmSocket;

        private ByteArraySegment m_outBuffer;
        private int m_outBufferSize;

        private int m_writeSize;

        private enum State
        {
            Idle,
            Delaying,
            Connecting,
            Active,
            ActiveSendingIdle,
            Error
        }

        private State m_state;
        private PgmAddress m_pgmAddress;
        private SessionBase m_session;
        private int m_currentReconnectIvl;

        public PgmSender([NotNull] IOThread ioThread, [NotNull] Options options, [NotNull] Address addr, bool delayedStart)
            : base(ioThread)
        {
            m_options = options;
            m_addr = addr;
            m_delayedStart = delayedStart;
            m_encoder = null;
            m_outBuffer = null;
            m_outBufferSize = 0;
            m_writeSize = 0;
            m_encoder = new V1Encoder(0, m_options.Endian);
            m_currentReconnectIvl = m_options.ReconnectIvl;

            m_state = State.Idle;
        }

        public void Init([NotNull] PgmAddress pgmAddress)
        {
            m_pgmAddress = pgmAddress;

            m_pgmSocket = new PgmSocket(m_options, PgmSocketType.Publisher, (PgmAddress)m_addr.Resolved);
            m_pgmSocket.Init();

            m_socket = m_pgmSocket.Handle;

            var localEndpoint = new IPEndPoint(IPAddress.Any, 0);

            m_socket.Bind(localEndpoint);

            m_pgmSocket.InitOptions();

            m_outBufferSize = m_options.PgmMaxTransportServiceDataUnitLength;
            m_outBuffer = new ByteArraySegment(new byte[m_outBufferSize]);
        }

        public void Plug(IOThread ioThread, SessionBase session)
        {
            m_session = session;
            m_encoder.SetMsgSource(session);

            // get the first message from the session because we don't want to send identities
            var msg = new Msg();
            msg.InitEmpty();

            bool ok = session.PullMsg(ref msg);

            if (ok)
            {
                msg.Close();
            }

            AddSocket(m_socket);

            if (!m_delayedStart)
            {
                StartConnecting();
            }
            else
            {
                m_state = State.Delaying;
                AddTimer(GetNewReconnectIvl(), ReconnectTimerId);
            }
        }

        private void StartConnecting()
        {
            m_state = State.Connecting;

            try
            {
                m_socket.Connect(m_pgmAddress.Address);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.InvalidArgument)
            {
                Error();
            }
        }

        public void Terminate()
        {
            Destroy();
        }

        public void ActivateOut()
        {
            if (m_state == State.ActiveSendingIdle)
            {
                m_state = State.Active;
                m_writeSize = 0;
                BeginSending();
            }
        }

        public void ActivateIn()
        {
            Debug.Assert(false);
        }

        /// <summary>
        /// This would be called when a timer expires, although here it only throws a NotSupportedException.
        /// </summary>
        /// <param name="id">an integer used to identify the timer (not used here)</param>
        /// <exception cref="NotImplementedException">This method must not be called on instances of PgmSender.</exception>
        public override void TimerEvent(int id)
        {
            if (m_state == State.Delaying)
            {
                StartConnecting();
            }
            else
            {
                Debug.Assert(false);
            }
        }

        /// <summary>
        /// This method is called when a message Send operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        /// <exception cref="NetMQException">A non-recoverable socket error occurred.</exception>
        public override void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            if (m_state == State.Connecting)
            {
                if (socketError == SocketError.Success)
                {
                    m_state = State.Active;
                    m_writeSize = 0;

                    BeginSending();
                }
                else
                {
                    m_state = State.Error;
                    NetMQException.Create(socketError);
                }
            }
            else if (m_state == State.Active)
            {
                // We can write either all data or 0 which means rate limit reached.
                if (socketError == SocketError.Success && bytesTransferred == m_writeSize)
                {
                    m_writeSize = 0;

                    BeginSending();
                }
                else
                {
                    if (socketError == SocketError.ConnectionReset)
                        Error();
                    else
                        throw NetMQException.Create(socketError.ToErrorCode());
                }
            }
            else
            {
                Debug.Assert(false);
            }
        }

        private void BeginSending()
        {
            // If write buffer is empty,  try to read new data from the encoder.
            if (m_writeSize == 0)
            {
                // First two bytes (sizeof uint16_t) are used to store message
                // offset in following steps. Note that by passing our buffer to
                // the get data function we prevent it from returning its own buffer.
                var bf = new ByteArraySegment(m_outBuffer, sizeof(ushort));
                int bfsz = m_outBufferSize - sizeof(ushort);
                int offset = -1;
                m_encoder.GetData(ref bf, ref bfsz, ref offset);

                // If there are no data to write stop polling for output.
                if (bfsz == 0)
                {
                    m_state = State.ActiveSendingIdle;
                    return;
                }

                // Put offset information in the buffer.
                m_writeSize = bfsz + sizeof(ushort);

                m_outBuffer.PutUnsignedShort(m_options.Endian, offset == -1 ? (ushort)0xffff : (ushort)offset, 0);
            }

            try
            {
                m_socket.Send((byte[])m_outBuffer, m_outBuffer.Offset, m_writeSize, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionReset)
                    Error();
                else
                    throw NetMQException.Create(ex.SocketErrorCode, ex);
            }
        }

        private void Error()
        {
            Debug.Assert(m_session != null);
            m_session.Detach();
            Destroy();
        }

        private void Destroy()
        {
            if (m_state == State.Delaying)
            {
                CancelTimer(ReconnectTimerId);
            }

            m_pgmSocket.Dispose();
            RemoveSocket(m_socket);
            m_encoder.SetMsgSource(null);
        }

        /// <summary>
        /// Internal function to return a reconnect back-off delay.
        /// Will modify the current_reconnect_ivl used for next call
        /// Returns the currently used interval
        /// </summary>
        private int GetNewReconnectIvl()
        {
            // The new interval is the current interval + random value.
            int thisInterval = m_currentReconnectIvl + new Random().Next(0, m_options.ReconnectIvl);

            // Only change the current reconnect interval  if the maximum reconnect
            // interval was set and if it's larger than the reconnect interval.
            if (m_options.ReconnectIvlMax > 0 &&
                m_options.ReconnectIvlMax > m_options.ReconnectIvl)
            {
                // Calculate the next interval
                m_currentReconnectIvl = m_currentReconnectIvl * 2;
                if (m_currentReconnectIvl >= m_options.ReconnectIvlMax)
                {
                    m_currentReconnectIvl = m_options.ReconnectIvlMax;
                }
            }
            return thisInterval;
        }

        /// <summary>
        /// This method would be called when a message receive operation has been completed, although here it only throws a NotSupportedException.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        /// <exception cref="NotImplementedException">This method must not be called on instances of PgmSender.</exception>
        public override void InCompleted(SocketError socketError, int bytesTransferred)
        {
            throw new NotImplementedException();
        }
    }
}
