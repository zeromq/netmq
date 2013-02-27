using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetMQ.zmq
{
	class PgmListener : Own, IPollEvents
	{
		private PgmSocket m_pgmSocket;

		private readonly SocketBase m_socket;
		
		private Socket m_handle;

		private readonly IOObject m_ioObject;

		private PgmAddress m_address;

		public PgmListener(IOThread ioThread, SocketBase socket, Options options)
			: base(ioThread, options)
		{
			m_socket = socket;
			
			m_ioObject = new IOObject(ioThread);
		}

		public void Init(string network)
		{
			m_address = new PgmAddress(network);

			m_pgmSocket = new PgmSocket(m_options, PgmSocketType.Listener, m_address);
			m_pgmSocket.Init();
			
			m_handle = m_pgmSocket.FD;

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
		{

		}

		protected override void ProcessPlug()
		{
			//  Start polling for incoming connections.
			m_ioObject.SetHandler(this);
			m_ioObject.AddFd(m_handle);
			m_ioObject.SetPollin(m_handle);
			m_ioObject.SetPollout(m_handle);
		}

		protected override void ProcessTerm(int linger)
		{
			m_ioObject.SetHandler(this);
			m_ioObject.RmFd(m_handle);
			Close();
			base.ProcessTerm(linger);
		}

		private void Close()
		{
			if (m_handle == null)
				return;

			try
			{
				m_handle.Close();
				m_socket.EventClosed(m_address.ToString(), m_handle);
			}
			catch (SocketException ex)
			{				
				m_socket.EventCloseFailed(m_address.ToString(), ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode));
			}
			catch (NetMQException ex)
			{
				//ZError.exc (e);
				m_socket.EventCloseFailed(m_address.ToString(), ex.ErrorCode);
			}
			m_handle = null;

		}


		public void InEvent()
		{
			Socket fd;

			try
			{
				fd = Accept();
			}
			catch (SocketException ex)
			{
				m_socket.EventAcceptFailed(m_address.ToString(), ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode));
				return;
			}
			catch (NetMQException ex)
			{
				//  If connection was reset by the peer in the meantime, just ignore it.
				//  TODO: Handle specific errors like ENFILE/EMFILE etc.
				//ZError.exc (e);
				m_socket.EventAcceptFailed(m_address.ToString(), ex.ErrorCode);
				return;
			}

			PgmSocket pgmSocket = new PgmSocket(m_options, PgmSocketType.Receiver, m_address);
			pgmSocket.Init(fd);

			PgmSession pgmSession = new PgmSession(pgmSocket, m_options);

			IOThread ioThread = ChooseIOThread(m_options.Affinity);

			SessionBase session = SessionBase.Create(ioThread, false, m_socket,
																							 m_options, new Address(m_handle.LocalEndPoint));
			session.IncSeqnum();
			LaunchChild(session);
			SendAttach(session, pgmSession, false);
			m_socket.EventAccepted(m_address.ToString(), fd);		
		}

		private Socket Accept()
		{
			Socket socket;

			try
			{
				socket = m_handle.Accept();
				socket.Blocking = false;
				m_pgmSocket.InitOptions();
			}
			catch (SocketException)
			{
				return null;
			}

			return socket;
		}

		public void OutEvent()
		{
			throw new NotSupportedException();
		}

		public void TimerEvent(int id)
		{
			throw new NotSupportedException();
		}
	}
}
