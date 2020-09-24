using System;
using System.Net.Sockets;
using AsyncIO;

namespace NetMQ.Core.Transports.Pgm
{
    internal class PgmListener : Own, IProactorEvents
    {
        private readonly SocketBase m_socket;
        private readonly IOObject m_ioObject;
        private AsyncSocket? m_handle;
        private PgmSocket? m_pgmSocket;
        private PgmSocket? m_acceptedSocket;
        private PgmAddress? m_address;

        public PgmListener(IOThread ioThread, SocketBase socket, Options options)
            : base(ioThread, options)
        {
            m_socket = socket;

            m_ioObject = new IOObject(ioThread);
        }

        /// <exception cref="InvalidException">Unable to parse the address's port number, or the IP address could not be parsed.</exception>
        /// <exception cref="NetMQException">Error establishing underlying socket.</exception>
        public void Init(string network)
        {
            m_address = new PgmAddress(network);

            m_pgmSocket = new PgmSocket(m_options, PgmSocketType.Listener, m_address);
            m_pgmSocket.Init();

            m_handle = m_pgmSocket.Handle;

            Assumes.NotNull(m_handle);

            try
            {
                m_handle.Bind(m_address.Address);
                m_pgmSocket.InitOptions();
                m_handle.Listen(m_options.Backlog);
            }
            catch (SocketException ex)
            {
                Close();

                throw NetMQException.Create(ex);
            }

            m_socket.EventListening(m_address.ToString(), m_handle);
        }

        public override void Destroy()
        {}

        protected override void ProcessPlug()
        {
            Assumes.NotNull(m_handle);

            // Start polling for incoming connections.
            m_ioObject.SetHandler(this);
            m_ioObject.AddSocket(m_handle);

            Accept();
        }

        /// <summary>
        /// Process a termination request.
        /// </summary>
        /// <param name="linger">a time (in milliseconds) for this to linger before actually going away. -1 means infinite.</param>
        protected override void ProcessTerm(int linger)
        {
            Assumes.NotNull(m_handle);

            m_ioObject.SetHandler(this);
            m_ioObject.RemoveSocket(m_handle);
            Close();
            base.ProcessTerm(linger);
        }

        private void Close()
        {
            if (m_handle == null)
                return;

            Assumes.NotNull(m_address);

            try
            {
                m_handle.Dispose();
                m_socket.EventClosed(m_address.ToString(), m_handle);
            }
            catch (SocketException ex)
            {
                m_socket.EventCloseFailed(m_address.ToString(), ex.SocketErrorCode.ToErrorCode());
            }
            catch (NetMQException ex)
            {
                m_socket.EventCloseFailed(m_address.ToString(), ex.ErrorCode);
            }

            m_handle = null;
        }

        /// <summary>
        /// This method is called when a message receive operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            Assumes.NotNull(m_address);
            Assumes.NotNull(m_acceptedSocket);
            Assumes.NotNull(m_acceptedSocket.Handle);

            if (socketError != SocketError.Success)
            {
                m_socket.EventAcceptFailed(m_address.ToString(), socketError.ToErrorCode());

                // dispose old object
                m_acceptedSocket.Handle.Dispose();

                Accept();
            }
            else
            {
                //This if-case only concerns bound PGM Subscribers after the Ethernet cable has been unplugged (Publisher on same host)
                //or plugged in again (Publisher on different host).
                if (m_address.InterfaceAddress != null)
                {
                    try
                    {
                        m_acceptedSocket.Handle.SetSocketOption(PgmSocket.PgmLevel, PgmSocket.RM_ADD_RECEIVE_IF,
                            m_address.InterfaceAddress.GetAddressBytes());
                    }
                    catch
                    {
                        // dispose old object
                        m_acceptedSocket.Handle.Dispose();

                        Accept();
                        return;
                    }
                }

                m_acceptedSocket.InitOptions();

                var pgmSession = new PgmSession(m_acceptedSocket, m_options);

                IOThread? ioThread = ChooseIOThread(m_options.Affinity);

                Assumes.NotNull(ioThread);
                Assumes.NotNull(m_handle);

                SessionBase session = SessionBase.Create(ioThread, false, m_socket, m_options, new Address(m_handle.LocalEndPoint));

                session.IncSeqnum();
                LaunchChild(session);
                SendAttach(session, pgmSession, false);
                m_socket.EventAccepted(m_address.ToString(), m_acceptedSocket.Handle);

                Accept();
            }
        }

        private void Accept()
        {
            Assumes.NotNull(m_address);

            m_acceptedSocket = new PgmSocket(m_options, PgmSocketType.Receiver, m_address);
            m_acceptedSocket.Init();

            Assumes.NotNull(m_handle);

#pragma warning disable CS0618 // Type or member is obsolete
            // TODO: we must upgrade to GetAcceptedSocket, need to be tested on Windows with MSMQ installed
            m_handle.Accept(m_acceptedSocket.Handle);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// This method would be called when a message Send operation has been completed, although here it only throws a NotSupportedException.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        /// <exception cref="NotSupportedException">This operation is not supported on the PgmListener class.</exception>
        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This would be called when the a expires, although here it only throws a NotSupportedException.
        /// </summary>
        /// <param name="id">an integer used to identify the timer (not used here)</param>
        /// <exception cref="NotSupportedException">This operation is not supported on the PgmListener class.</exception>
        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}