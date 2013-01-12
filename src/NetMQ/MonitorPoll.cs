using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.zmq;

namespace NetMQ
{
	/// <summary>
	/// Use the  class when you want to monitor socket
	/// </summary>
	public class MonitorPoll
	{
		IMonitoringEventsHandler m_monitoringEventsHandler;
		bool m_initialized = false;

		MonitorEvent m_monitorEvent;
		CancellationTokenSource m_cancellationTokenSource = new CancellationTokenSource();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context">The context of the socket</param>
		/// <param name="socket">Socket to monitor</param>
		/// <param name="address">Monitoring address to use</param>
		/// <param name="monitoringEventsHandler">The events handler interface</param>
		public MonitorPoll(Context context, BaseSocket socket,
			string address, IMonitoringEventsHandler monitoringEventsHandler)
		{
			Socket = socket;
			MonitorAddress = address;
			m_monitoringEventsHandler = monitoringEventsHandler;
			Context = context;
			Timeout = TimeSpan.FromSeconds(1);
		}

		/// <summary>
		/// The monitoring address
		/// </summary>
		public string MonitorAddress { get; private set; }

		/// <summary>
		/// The socket to monitor
		/// </summary>
		public BaseSocket Socket { get; private set; }

		/// <summary>
		/// The socket context
		/// </summary>
		public Context Context { get; private set; }

		/// <summary>
		/// Monitoring socket created by the init method
		/// </summary>
		internal BaseSocket MonitoringSocket { get; private set; }

		/// <summary>
		/// How much time to wait on each poll iteration, the higher the number the longer it will take the poller to stop 
		/// </summary>
		public TimeSpan Timeout { get; set; }


		/// <summary>
		/// Creating the monitoring socket, this method automaticlly called by the start method
		/// if you want to receive events that happen before the call to the start mehod call
		/// this method earlier in your code
		/// </summary>
		public void Init()
		{
			if (!m_initialized)
			{
				// in case the sockets is created in another thread
				Thread.MemoryBarrier();

				ZMQ.SocketMonitor(Socket.SocketHandle, MonitorAddress, m_monitoringEventsHandler.Events);

				MonitoringSocket = Context.CreatePairSocket();
				MonitoringSocket.Options.Linger = TimeSpan.Zero;

				MonitoringSocket.Connect(MonitorAddress);

				m_initialized = true;
			}
		}

		internal void Handle()
		{
			MonitorEvent monitorEvent = MonitorEvent.Read(MonitoringSocket.SocketHandle);

			if (monitorEvent != null)
			{
				switch (monitorEvent.Event)
				{
					case SocketEvent.Connected:
						m_monitoringEventsHandler.OnConnected(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);
						break;
					case SocketEvent.ConnectDelayed:
						m_monitoringEventsHandler.OnConnectFailed(monitorEvent.Addr, (ErrorCode)monitorEvent.Arg);
						break;
					case SocketEvent.ConnectRetried:
						m_monitoringEventsHandler.OnConnectRetried(monitorEvent.Addr, (int)monitorEvent.Arg);
						break;
					case SocketEvent.ConnectFailed:
						m_monitoringEventsHandler.OnConnectFailed(monitorEvent.Addr, (ErrorCode)monitorEvent.Arg);
						break;
					case SocketEvent.Listening:
						m_monitoringEventsHandler.OnListening(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);
						break;
					case SocketEvent.BindFailed:
						m_monitoringEventsHandler.OnBindFailed(monitorEvent.Addr, (ErrorCode)monitorEvent.Arg);
						break;
					case SocketEvent.Accepted:
						m_monitoringEventsHandler.OnAccepted(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);
						break;
					case SocketEvent.AcceptFailed:
						m_monitoringEventsHandler.OnAcceptFailed(monitorEvent.Addr, (ErrorCode)monitorEvent.Arg);
						break;
					case SocketEvent.Closed:
						m_monitoringEventsHandler.OnClosed(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);
						break;
					case SocketEvent.CloseFailed:
						m_monitoringEventsHandler.OnCloseFailed(monitorEvent.Addr, (ErrorCode)monitorEvent.Arg);
						break;
					case SocketEvent.Disconnected:
						m_monitoringEventsHandler.OnDisconnected(monitorEvent.Addr, (IntPtr)monitorEvent.Arg);
						break;

					default:
						throw new Exception("unknown event " + monitorEvent.Event.ToString());
						break;
				}
			}
		}

		/// <summary>
		/// Start monitor the socket, the methdo doesn't start a new thread and will block until the monitor poll is stopped
		/// </summary>
		public void Start()
		{
			Init();

			// in case the sockets is created in another thread
			Thread.MemoryBarrier();

			while (!m_cancellationTokenSource.IsCancellationRequested)
			{
				if (MonitoringSocket.Poll(Timeout, PollEvents.PollIn))
				{
					Handle();
				}
			}

			MonitoringSocket.Close();
		}

		// Stop the socket monitoring
		public void Stop()
		{
			m_cancellationTokenSource.Cancel();
		}
	}
}
