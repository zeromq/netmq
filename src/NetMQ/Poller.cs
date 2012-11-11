using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.zmq;

namespace NetMQ
{
	public class Poller
	{
		
		class ProxyPoll
		{
			public BaseSocket FromSocket { get; set; }

			public BaseSocket ToSocket { get; set; }
		}

		class SocketPoll
		{
			public BaseSocket Socket { get; set; }
			public Action<BaseSocket> Action;
		}

		readonly CancellationTokenSource m_cancellationTokenSource;
		readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);

		readonly IList<SocketPoll> m_sockets = new List<SocketPoll>();
		readonly IList<ProxyPoll> m_proxies = new List<ProxyPoll>();
		readonly IList<MonitorPoll> m_monitors = new List<MonitorPoll>();
		private readonly Context m_context;

		public Poller(Context context)
		{
			m_context = context;
			CloseSocketsOnStop = true;
			PollTimeout = TimeSpan.FromSeconds(1);
			m_cancellationTokenSource = new CancellationTokenSource();
		}

		/// <summary>
		/// If true the poller will close all sockets when the poller is stopped
		/// </summary>
		public bool CloseSocketsOnStop { get; set; }

		/// <summary>
		/// How much time to wait on each poll iteration, the higher the number the longer it will take the poller to stop 
		/// </summary>
		public TimeSpan PollTimeout { get; set; }

		private void CheckSocketAlreadyExist(BaseSocket baseSocket)
		{
			if (m_sockets.Select(s => s.Socket).Contains(baseSocket) || m_proxies.Select(p => p.FromSocket).Contains(baseSocket))
			{
				throw new NetMQException("Socket already exist");
			}
		}

		/// <summary>
		/// Add socket the poller
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="socket">Socket to listen to</param>
		/// <param name="action">The action will be called when message is ready to be received</param>
		public void AddSocket<T>(T socket, Action<T> action) where T : BaseSocket
		{
			CheckSocketAlreadyExist(socket);

			m_sockets.Add(new SocketPoll
				{
					Socket = socket,
					Action = s => action(s as T)
				});

		}

		/// <summary>
		/// Add proxy to the poller, the proxy will forward messages from one socket to another socket
		/// If duplex is true the socket will forward messages the opposite way as well
		/// </summary>
		/// <param name="from">The messages from this socket will forward</param>
		/// <param name="to">This socket will receive the messages from the from socket</param>
		/// <param name="duplex">If true the messages will both ways</param>	
		public void AddProxy(BaseSocket from, BaseSocket to, bool duplex)
		{
			CheckSocketAlreadyExist(from);

			if (duplex)
			{
				CheckSocketAlreadyExist(to);
			}

			m_proxies.Add(new ProxyPoll { FromSocket = from, ToSocket = to });

			if (duplex)
			{
				m_proxies.Add(new ProxyPoll { FromSocket = to, ToSocket = from });
			}
		}		

		public void AddMonitor(BaseSocket socket, string address, IMonitoringEventsHandler eventsHandler)
		{
			AddMonitor(socket, address, eventsHandler, true);
		}

		public void AddMonitor(BaseSocket socket, string address, IMonitoringEventsHandler eventsHandler, bool init)
		{
			MonitorPoll monitorPoll = new MonitorPoll(m_context, socket, address, eventsHandler);

			m_monitors.Add(monitorPoll);
			
			if (init)
			{
				monitorPoll.Init();
			}
		}

		/// <summary>
		/// Start the poller job, the start doesn't create a new thread. This method will block until stop is called
		/// In the begining of the method full memory barried is called in case the sockets was created in another thread
		/// </summary>
		public void Start()
		{
			m_isStoppedEvent.Reset();

			// the sockets may have been created in another thread, to make sure we can fully use them we do full memory barried
			// at the begining of the loop
			Thread.MemoryBarrier();

			PollItem[] items = new PollItem[m_proxies.Count + m_sockets.Count + m_monitors.Count];
			int i = 0;

			foreach (var item in m_sockets)
			{
				items[i] = new PollItem(item.Socket.SocketHandle, PollEvents.PollIn);
				i++;
			}

			foreach (var item in m_proxies)
			{
				items[i] = new PollItem(item.FromSocket.SocketHandle, PollEvents.PollIn);
				i++;
			}

			Dictionary<SocketBase, SocketPoll> handleToSocket = m_sockets.ToDictionary(k => k.Socket.SocketHandle, v => v);
			Dictionary<SocketBase, ProxyPoll> handleToProxy = m_proxies.ToDictionary(k => k.FromSocket.SocketHandle, v => v);

			foreach (MonitorPoll poll in m_monitors)
			{
				poll.Init();
				items[i] = new PollItem(poll.MonitoringSocket.SocketHandle, PollEvents.PollIn);
				i++;
			}

			Dictionary<SocketBase, MonitorPoll> handleToMonitor = m_monitors.ToDictionary(k => k.MonitoringSocket.SocketHandle, m => m);

			while (!m_cancellationTokenSource.IsCancellationRequested)
			{
				int result = ZMQ.Poll(items, (int)PollTimeout.TotalMilliseconds);

				foreach (var item in items)
				{
					if ((item.ResultEvent & PollEvents.PollIn) == PollEvents.PollIn)
					{
						SocketPoll socketPoll;
						ProxyPoll proxyPoll;
						MonitorPoll monitorPoll;

						if (handleToSocket.TryGetValue(item.Socket, out socketPoll))
						{
							// calling the registed delegate to do the read from the socket
							socketPoll.Action(socketPoll.Socket);
						}
						else if (handleToProxy.TryGetValue(item.Socket, out proxyPoll))
						{
							Msg msg = ZMQ.Recv(proxyPoll.FromSocket.SocketHandle, SendRecieveOptions.None);

							bool more = ZMQ.ZmqMsgGet(msg, MsgFlags.More) == 1;

							ZMQ.Send(proxyPoll.ToSocket.SocketHandle, msg, more ? SendRecieveOptions.SendMore : SendRecieveOptions.None);

							while (more)
							{
								msg = ZMQ.Recv(proxyPoll.FromSocket.SocketHandle, SendRecieveOptions.None);

								more = ZMQ.ZmqMsgGet(msg, MsgFlags.More) == 1;

								ZMQ.Send(proxyPoll.ToSocket.SocketHandle, msg, more ? SendRecieveOptions.SendMore : SendRecieveOptions.None);
							}
						}
						else if (handleToMonitor.TryGetValue(item.Socket, out monitorPoll))
						{
							monitorPoll.Handle();
						}
					}
				}
			}

			// we should close the monitors anyway because this poller created them
			foreach (var monitorPoll in m_monitors)
			{
				monitorPoll.MonitoringSocket.Close();
			}

			if (CloseSocketsOnStop)
			{
				// make a list of all the sockets the poller need to close
				var socketToClose =
					m_sockets.Select(m => m.Socket).Union(m_proxies.Select(p => p.FromSocket)).
						Union(m_proxies.Select(p => p.ToSocket)).Distinct();						

				foreach (var socket in socketToClose)
				{
					socket.Close();
				}
			}
		
			// the poller is stopped
			m_isStoppedEvent.Set();
		}

		/// <summary>
		/// Stop the poller job, it may take a while until the poller is fully stopped
		/// </summary>
		/// <param name="waitForCloseToComplete">if true the method will block until the poller is fully stopped</param>
		public void Stop(bool waitForCloseToComplete)
		{
			m_cancellationTokenSource.Cancel();

			if (waitForCloseToComplete)
			{
				m_isStoppedEvent.WaitOne();
			}
		}

		public void Stop()
		{
			Stop(true);
		}
	}
}
