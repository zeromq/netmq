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
using System.Net.Sockets;
using System.Diagnostics;

//  If 'delay' is true connecter first waits for a while, then starts
//  connection process.
namespace zmq
{
	public class TcpConnecter : Own, IPollEvents
	{

		//private static Logger LOG = LoggerFactory.getLogger(TcpConnecter.class);

		//  ID of the timer used to delay the reconnection.
		private const int ReconnectTimerId = 1;

		private readonly IOObject m_ioObject;

		//  Address to connect to. Owned by session_base_t.
		private readonly Address m_addr;

		//  Underlying socket.
		private Socket m_handle;

		//  If true file descriptor is registered with the poller and 'handle'
		//  contains valid value.
		private bool m_handleValid;

		//  If true, connecter is waiting a while before trying to connect.
		private readonly bool m_delayedStart;

		//  True iff a timer has been started.
		private bool m_timerStarted;

		//  Reference to the session we belong to.
		private readonly SessionBase m_session;

		//  Current reconnect ivl, updated for backoff strategy
		private int m_currentReconnectIvl;

		// String representation of endpoint to connect to
		private readonly String m_endpoint;

		// Socket
		private readonly SocketBase m_socket;

		public TcpConnecter(IOThread ioThread,
		                    SessionBase session, Options options,
		                    Address addr, bool delayedStart)
			: base(ioThread, options)
		{


			m_ioObject = new IOObject(ioThread);
			m_addr = addr;
			m_handle = null;
			m_handleValid = false;
			m_delayedStart = delayedStart;
			m_timerStarted = false;
			m_session = session;
			m_currentReconnectIvl = m_options.ReconnectIvl;

			Debug.Assert(m_addr != null);
			m_endpoint = m_addr.ToString();
			m_socket = session.Socket;
		}

		public override void Destroy()
		{
			Debug.Assert(!m_timerStarted);
			Debug.Assert(!m_handleValid);
			Debug.Assert(m_handle == null);
		}


		protected override void ProcessPlug()
		{
			m_ioObject.SetHandler(this);
			if (m_delayedStart)
				AddReconnectTimer();
			else
			{
				StartConnecting();
			}
		}


		protected override void ProcessTerm(int linger)
		{
			if (m_timerStarted)
			{
				m_ioObject.CancelTimer(ReconnectTimerId);
				m_timerStarted = false;
			}

			if (m_handleValid)
			{
				m_ioObject.RmFd(m_handle);
				m_handleValid = false;
			}

			if (m_handle != null)
				Close();

			base.ProcessTerm(linger);
		}

		public void InEvent()
		{
			// connected but attaching to stream engine is not completed. do nothing
			OutEvent();
		}

		public void OutEvent()
		{
			// connected but attaching to stream engine is not completed. do nothing

			bool err = false;
			Socket fd = null;
			try
			{
				fd = Connect();
			}
				//catch (ConnectException e) {
				//    err = true;
				//} 
			catch (SocketException)
			{
				err = true;
			}
			//catch (IOException e) {
			//    throw new ZError.IOException(e);
			//}

			m_ioObject.RmFd(m_handle);
			m_handleValid = false;

			if (err)
			{
				//  Handle the error condition by attempt to reconnect.
				Close();
				AddReconnectTimer();
				return;
			}

			m_handle = null;

			try
			{

				Utils.TuneTcpSocket(fd);
				Utils.TuneTcpKeepalives(fd, m_options.TcpKeepalive, m_options.TcpKeepaliveCnt, m_options.TcpKeepaliveIdle, m_options.TcpKeepaliveIntvl);
			}
			catch (SocketException)
			{
				throw new Exception();
			}

			//  Create the engine object for this connection.
			StreamEngine engine;
			try
			{
				engine = new StreamEngine(fd, m_options, m_endpoint);
			}
			catch (SocketException)
			{
				//LOG.error("Failed to initialize StreamEngine", e.getCause());
				m_socket.EventConnectDelayed(m_endpoint, ZError.ErrorNumber);
				return;
			}
			//alloc_Debug.Assert(engine);

			//  Attach the engine to the corresponding session object.
			SendAttach(m_session, engine);

			//  Shut the connecter down.
			Terminate();

			m_socket.EventConnected(m_endpoint, fd);
		}

		public void TimerEvent(int id)
		{
			m_timerStarted = false;
			StartConnecting();
		}

		//  Internal function to start the actual connection establishment.
		private void StartConnecting()
		{
			//  Open the connecting socket.

			try
			{
				bool rc = Open();

				//  Connect may succeed in synchronous manner.
				if (rc)
				{
					m_ioObject.AddFd(m_handle);
					m_handleValid = true;
					m_ioObject.OutEvent();
				}

					//  Connection establishment may be delayed. Poll for its completion.
				else
				{
					m_ioObject.AddFd(m_handle);
					m_handleValid = true;
					m_ioObject.SetPollout(m_handle);
					m_socket.EventConnectDelayed(m_endpoint, ZError.ErrorNumber);
				}
			}
			catch (SocketException)
			{
				//  Handle any other error condition by eventual reconnect.
				if (m_handle != null)
					Close();
				AddReconnectTimer();
			}
		}

		//  Internal function to add a reconnect timer
		private void AddReconnectTimer()
		{
			int rcIvl = GetNewReconnectIvl();
			m_ioObject.AddTimer(rcIvl, ReconnectTimerId);
			m_socket.EventConnectRetried(m_endpoint, rcIvl);
			m_timerStarted = true;
		}

		//  Internal function to return a reconnect backoff delay.
		//  Will modify the current_reconnect_ivl used for next call
		//  Returns the currently used interval
		private int GetNewReconnectIvl()
		{
			//  The new interval is the current interval + random value.
			int thisInterval = m_currentReconnectIvl +
			                    (Utils.GenerateRandom() % m_options.ReconnectIvl);

			//  Only change the current reconnect interval  if the maximum reconnect
			//  interval was set and if it's larger than the reconnect interval.
			if (m_options.ReconnectIvlMax > 0 &&
			    m_options.ReconnectIvlMax > m_options.ReconnectIvl)
			{

				//  Calculate the next interval
				m_currentReconnectIvl = m_currentReconnectIvl * 2;
				if (m_currentReconnectIvl >= m_options.ReconnectIvlMax)
				{
					m_currentReconnectIvl = m_options.ReconnectIvlMax;
				}
			}
			return thisInterval;
		}

		//  Open TCP connecting socket. Returns -1 in case of error,
		//  true if connect was successfull immediately. Returns false with
		//  if async connect was launched.
		private bool Open()
		{
			Debug.Assert(m_handle == null);

			//  Create the socket.
			m_handle = new Socket(m_addr.Resolved.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			m_handle.Blocking = false;

			// Set the socket to non-blocking mode so that we get async connect().
			//Utils.unblock_socket(handle);

			//  Connect to the remote peer.
			try
			{
				m_handle.Connect(m_addr.Resolved.Address.Address.ToString(), m_addr.Resolved.Address.Port);
			}
			catch (SocketException ex)
			{
				if (ex.SocketErrorCode == SocketError.WouldBlock || ex.SocketErrorCode == SocketError.InProgress)
				{
					ZError.ErrorNumber = ErrorNumber.EINPROGRESS;
				}

				return false;
			}
			return true;
		}

		//  Get the file descriptor of newly created connection. Returns
		//  retired_fd if the connection was unsuccessfull.
		private Socket Connect()
		{
			bool finished = m_handle.Connected;
			Debug.Assert(finished);
			//SocketChannel ret = handle;

			return m_handle;
		}

		//  Close the connecting socket.
		private void Close()
		{
			Debug.Assert(m_handle != null);
			try
			{
				m_handle.Close();
				m_socket.EventClosed(m_endpoint, m_handle);
				m_handle = null;
			}
			catch (SocketException)
			{
				//ZError.exc (e);
				m_socket.EventCloseFailed(m_endpoint, ZError.ErrorNumber);
			}

		}
	}
}
