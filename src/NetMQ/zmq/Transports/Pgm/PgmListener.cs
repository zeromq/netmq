using System;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.zmq.Transports.PGM
{
    internal class PgmListener : Own, IProactorEvents
    {
        [NotNull] private readonly SocketBase m_socket;
        [NotNull] private readonly IOObject m_ioObject;
        private AsyncSocket m_handle;
        private PgmSocket m_pgmSocket;
        private PgmSocket m_acceptedSocket;
        private PgmAddress m_address;

        public PgmListener([NotNull] IOThread ioThread, [NotNull] SocketBase socket, [NotNull] Options options)
            : base(ioThread, options)
        {
            m_socket = socket;

            m_ioObject = new IOObject(ioThread);
        }

        public void Init([NotNull] string network)
        {
            m_address = new PgmAddress(network);

            m_pgmSocket = new PgmSocket(m_options, PgmSocketType.Listener, m_address);
            m_pgmSocket.Init();

            m_handle = m_pgmSocket.Handle;

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
            //  Start polling for incoming connections.
            m_ioObject.SetHandler(this);
            m_ioObject.AddSocket(m_handle);

            Accept();
        }

        protected override void ProcessTerm(int linger)
        {
            m_ioObject.SetHandler(this);
            m_ioObject.RemoveSocket(m_handle);
            Close();
            base.ProcessTerm(linger);
        }

        private void Close()
        {
            if (m_handle == null)
                return;

            try
            {
                m_handle.Dispose();
                m_socket.EventClosed(m_address.ToString(), m_handle);
            }
            catch (SocketException ex)
            {
                m_socket.EventCloseFailed(m_address.ToString(), ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode));
            }
            catch (NetMQException ex)
            {
                m_socket.EventCloseFailed(m_address.ToString(), ex.ErrorCode);
            }

            m_handle = null;
        }


        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success)
            {
                m_socket.EventAcceptFailed(m_address.ToString(), ErrorHelper.SocketErrorToErrorCode(socketError));

                // dispose old object                
                m_acceptedSocket.Handle.Dispose();

                Accept();
            }
            else
            {
                m_acceptedSocket.InitOptions();

                var pgmSession = new PgmSession(m_acceptedSocket, m_options);

                IOThread ioThread = ChooseIOThread(m_options.Affinity);

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
            m_acceptedSocket = new PgmSocket(m_options, PgmSocketType.Receiver, m_address);
            m_acceptedSocket.Init();

            m_handle.Accept(m_acceptedSocket.Handle);
        }

        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            throw new NotSupportedException();
        }

        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}