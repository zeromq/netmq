using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public delegate void FDDelegate(string address, IntPtr fd);
	public delegate void ErrorDelegate(string address, ErrorCode errorCode);
	public delegate void IntervalDelegate(string address, int interval);

	/// <summary>
	/// Use this class instead of implementing the IMonitoringEventsHandler.
	/// You can specify the handler for each of the different events
	/// </summary>
	public class MonitoringEventsHandler : IMonitoringEventsHandler
	{
		FDDelegate m_onConnected;
		ErrorDelegate m_onConnectDelayed;
		IntervalDelegate m_onConnectionRetried;
		ErrorDelegate m_onConnectFailed;
		FDDelegate m_onListening;
		ErrorDelegate m_onBindFailed;
		FDDelegate m_onAccepted;
		ErrorDelegate m_onAcceptFailed;
		FDDelegate m_onClosed;
		ErrorDelegate m_onClosedFailed;
		FDDelegate m_onDisconneted;

		/// <summary>
		/// Set the action when the socket is connected
		/// </summary>
		public FDDelegate OnConnected
		{
			set
			{
				m_onConnected = value;
				Events |= SocketEvent.Connected;
			}
		}

		/// <summary>
		/// Set the action when the connect is delayed
		/// </summary>
		public ErrorDelegate OnConnectDelayed
		{
			set
			{
				m_onConnectDelayed = value;
				Events |= SocketEvent.ConnectDelayed;
			}
		}

		/// <summary>
		/// Set the action when the connection is retired
		/// </summary>
		public IntervalDelegate OnConnectionRetried
		{
			set
			{
				m_onConnectionRetried = value;
				Events |= SocketEvent.ConnectRetried;
			}
		}

		/// <summary>
		/// Set the action when the connect failed
		/// </summary>
		public ErrorDelegate OnConnectFailed
		{
			set
			{
				m_onConnectFailed = value;
				Events |= SocketEvent.ConnectFailed;
			}
		}

		/// <summary>
		/// Set the action when the socket is listening
		/// </summary>
		public FDDelegate OnListening
		{
			set
			{
				m_onListening = value;
				Events |= SocketEvent.Listening;
			}
		}

		/// <summary>
		/// Set the action when bind has failed
		/// </summary>
		public ErrorDelegate OnBindFailed
		{
			set
			{
				m_onBindFailed = value;
				Events |= SocketEvent.BindFailed;
			}
		}

		/// <summary>
		/// Set the action when new connection is accepted
		/// </summary>
		public FDDelegate OnAccepted
		{
			set
			{
				m_onAccepted = value;
				Events |= SocketEvent.Accepted;
			}
		}

		/// <summary>
		/// Set the action when accept has failed
		/// </summary>
		public ErrorDelegate OnAcceptFailed
		{
			set
			{
				m_onAcceptFailed = value;
				Events |= SocketEvent.AcceptFailed;
			}
		}
		
		/// <summary>
		/// Set the action when socket has closed
		/// </summary>
		public FDDelegate OnClosed
		{
			set
			{
				m_onClosed = value;
				Events |= SocketEvent.Closed;
			}
		}

		/// <summary>
		/// Set the action when close has failed
		/// </summary>
		public ErrorDelegate OnClosedFailed
		{
			set
			{
				m_onClosedFailed = value;
				Events |= SocketEvent.CloseFailed;
			}
		}

		/// <summary>
		/// Set th action when connection disconnect
		/// </summary>
		public FDDelegate OnDisconneted
		{
			set
			{
				m_onDisconneted = value;
				Events |= SocketEvent.Disconnected;
			}
		}

		/// <summary>
		/// The events the class will listen to, this property is automaticlly set by the action you set
		/// </summary>
		public SocketEvent Events { get; private set; }

		void IMonitoringEventsHandler.OnConnected(string address, IntPtr fd)
		{
			m_onConnected(address, fd);
		}

		void IMonitoringEventsHandler.OnConnectDelayed(string address, ErrorCode errorCode)
		{
			m_onConnectDelayed(address, errorCode);
		}

		void IMonitoringEventsHandler.OnConnectRetried(string address, int interval)
		{
			m_onConnectionRetried(address, interval);
		}

		void IMonitoringEventsHandler.OnConnectFailed(string address, ErrorCode errorCode)
		{
			m_onConnectFailed(address, errorCode);
		}

		void IMonitoringEventsHandler.OnListening(string address, IntPtr fd)
		{
			m_onListening(address, fd);
		}

		void IMonitoringEventsHandler.OnBindFailed(string address, ErrorCode errorCode)
		{
			m_onBindFailed(address, errorCode);
		}

		void IMonitoringEventsHandler.OnAccepted(string address, IntPtr fd)
		{
			m_onAccepted(address, fd);
		}

		void IMonitoringEventsHandler.OnAcceptFailed(string address, ErrorCode errorCode)
		{
			m_onAcceptFailed(address, errorCode);
		}

		void IMonitoringEventsHandler.OnClosed(string address, IntPtr fd)
		{
			m_onClosed(address, fd);
		}

		void IMonitoringEventsHandler.OnCloseFailed(string address, ErrorCode errorCode)
		{
			m_onClosedFailed(address, errorCode);
		}

		void IMonitoringEventsHandler.OnDisconnected(string address, IntPtr fd)
		{
			m_onDisconneted(address, fd);
		}
	}
}
