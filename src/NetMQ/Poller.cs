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
		public class TimerPoll
		{
			public Action<int> TimerAction { get; set; }
			public int Id { get; set; }
		}

		class ProxyPoll
		{
			public BaseSocket FromSocket { get; set; }

			public BaseSocket ToSocket { get; set; }
		}

		class SocketPoll
		{
			public BaseSocket Socket { get; set; }
			public Action<BaseSocket> Action;
			public bool Cancelled { get; set; }
		}

		readonly CancellationTokenSource m_cancellationTokenSource;
		readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);

		private bool m_socketRetired = false;
		private bool m_newSockets = false;

		readonly IList<SocketPoll> m_sockets = new List<SocketPoll>();
		readonly IList<ProxyPoll> m_proxies = new List<ProxyPoll>();
		readonly IList<MonitorPoll> m_monitors = new List<MonitorPoll>();

		readonly SortedList<long, IList<TimerPoll>> m_timers = new SortedList<long, IList<TimerPoll>>();


		private readonly Context m_context;
		private bool m_isStarted;

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

		/// <summary>
		/// Has the Poller been started.id =
		/// </summary>
		public bool IsStarted { get { return m_isStarted; } }

		private void CheckSocketAlreadyExist(BaseSocket baseSocket)
		{
			if (m_sockets.Select(s => s.Socket).Contains(baseSocket) || m_proxies.Select(p => p.FromSocket).Contains(baseSocket))
			{
				throw new ArgumentException("Socket already exist");
			}
		}

		public void AddTimer(long timeout, int id, Action<int> timerAction)
		{
			AddTimer(timeout, id, true, timerAction);
		}

		public void AddTimer(long timeout, int id, bool replaceExisting, Action<int> timerAction)
		{
			// checking if this id already exist
			if (m_timers.SelectMany(t=> t.Value).Select(t=> t.Id).Contains(id))
			{
				if (replaceExisting)
				{
					CancelTimer(id);
				}
				else
				{
					throw new ArgumentException(string.Format("Timer with id {0} already exist", id));
				}
			}

			long expiration = Clock.NowMs() + timeout;

			if (!m_timers.ContainsKey(expiration))
			{
				m_timers.Add(expiration, new List<TimerPoll>());
			}

			m_timers[expiration].Add(new TimerPoll
																 {
																	 Id = id,
																	 TimerAction = timerAction
																 });
		}

		public void CancelTimer(int id)
		{
			//  Complexity of this operation is O(n). We assume it is rarely used.
			var foundTimers = new Dictionary<long, TimerPoll>();

			foreach (var pair in m_timers)
			{
				var timer = pair.Value.FirstOrDefault(x => x.Id == id);

				if (timer == null)
					continue;

				if (!foundTimers.ContainsKey(pair.Key))
				{
					foundTimers[pair.Key] = timer;
					break;
				}
			}

			if (foundTimers.Count > 0)
			{
				foreach (var foundTimer in foundTimers)
				{
					if (m_timers[foundTimer.Key].Count == 1)
					{
						m_timers.Remove(foundTimer.Key);
					}
					else
					{
						m_timers[foundTimer.Key].Remove(foundTimer.Value);
					}
				}
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
					Action = s => action(s as T),
					Cancelled = false
				});

			m_newSockets = true;
		}

		public void CancelSocket<T>(T socket) where T : BaseSocket
		{
			SocketPoll poll = m_sockets.First(s => s.Socket == socket);

			if (poll != null)
			{
				m_sockets.Remove(poll);
				poll.Cancelled = true;
				m_socketRetired = true;
			}
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

		private int ExecuteTimers()
		{
			//  Fast track.
			if (!m_timers.Any())
				return 0;

			//  Get the current time.
			long current = Clock.NowMs();

			//  Execute the timers that are already due.
			var keys = m_timers.Keys;

			for (int i = 0; i < keys.Count; i++)
			{
				var key = keys[i];

				//  If we have to wait to execute the item, same will be true about
				//  all the following items (multimap is sorted). Thus we can stop
				//  checking the subsequent timers and return the time to wait for
				//  the next timer (at least 1ms).
				if (key > current)
				{
					return (int)(key - current);
				}

				var timers = m_timers[key];

				//  Trigger the timers.                
				foreach (var timer in timers)
				{
					timer.TimerAction(timer.Id);
				}

				//  Remove it from the list of active timers.		
				timers.Clear();
				m_timers.Remove(key);
				i--;
			}

			//  There are no more timers.
			return 0;
		}

		/// <summary>
		/// Start the poller job, the start doesn't create a new thread. This method will block until stop is called
		/// In the begining of the method full memory barried is called in case the sockets was created in another thread
		/// </summary>
		public void Start()
		{
			m_isStoppedEvent.Reset();
			m_isStarted = true;

			// the sockets may have been created in another thread, to make sure we can fully use them we do full memory barried
			// at the begining of the loop
			Thread.MemoryBarrier();

			int count = m_proxies.Count + m_sockets.Count + m_monitors.Count;

			PollItem[] items = new PollItem[count];
			int i = 0;

			// add all sockets to the poll array
			foreach (var item in m_sockets)
			{
				items[i] = new PollItem(item.Socket.SocketHandle, PollEvents.PollIn);
				i++;
			}

			// add all proxies to the poll array
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

			m_newSockets = false;
			m_socketRetired = false;

			int maxTimeout = (int)PollTimeout.TotalMilliseconds;

			while (!m_cancellationTokenSource.IsCancellationRequested)
			{
				int timeout = ExecuteTimers();

				if (timeout == 0 || timeout > maxTimeout)
				{
					timeout = maxTimeout;
				}

				int result = ZMQ.Poll(items, count, timeout);

				for (int j = 0; j < count; j++)
				{
					var item = items[j];

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

				if (m_socketRetired)
				{
					m_socketRetired = false;
					count = HandleSocketRetired(items, count, handleToSocket);
				}

				if (m_newSockets)
				{
					m_newSockets = false;
					count = HandleNewSockets(count, handleToSocket, ref items);
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
			m_isStarted = false;
		}

		private int HandleNewSockets(int count, Dictionary<SocketBase, SocketPoll> handleToSocket, ref PollItem[] items)
		{
			SocketPoll[] newSockets = m_sockets.Except(handleToSocket.Values).ToArray();

			int itemsNewSize = count + newSockets.Length;

			if (itemsNewSize > items.Length)
			{
				PollItem[] newArray = new PollItem[itemsNewSize];
				Array.Copy(items, newArray, count);

				items = newArray;
			}

			foreach (SocketPoll socketPoll in newSockets)
			{
				items[count] = new PollItem(socketPoll.Socket.SocketHandle, PollEvents.PollIn);
				handleToSocket.Add(socketPoll.Socket.SocketHandle, socketPoll);
				count++;
			}
			return count;
		}

		private static int HandleSocketRetired(PollItem[] items, int count, Dictionary<SocketBase, SocketPoll> handleToSocket)
		{
			SocketPoll[] retiredSockets = handleToSocket.Where(s => s.Value.Cancelled).Select(s => s.Value).ToArray();

			foreach (SocketPoll retiredSocket in retiredSockets)
			{
				handleToSocket.Remove(retiredSocket.Socket.SocketHandle);

				int currentIndex = 0;

				while (true)
				{
					if (items[currentIndex].Socket == retiredSocket.Socket.SocketHandle)
					{
						while (currentIndex < count - 1)
						{
							items[currentIndex] = items[currentIndex + 1];
							currentIndex++;
						}

						items[currentIndex] = null;

						count--;
						break;
					}

					currentIndex++;
				}
			}
			return count;
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
			m_isStarted = false;
		}

		public void Stop()
		{
			Stop(true);
		}
	}
}
