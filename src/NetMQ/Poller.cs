using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetMQ.zmq;

namespace NetMQ
{
	public class Poller
	{
		class PollerPollItem : PollItem
		{
			private NetMQSocket m_socket;

			public PollerPollItem(NetMQSocket socket, PollEvents events)
				: base(socket.SocketHandle, events)
			{
				m_socket = socket;
			}

			public NetMQSocket NetMQSocket
			{
				get { return m_socket; }
			}
		}

		private IList<NetMQSocket> m_sockets = new List<NetMQSocket>();
		IList<NetMQSocket> m_newSockets = new List<NetMQSocket>();
		IList<NetMQSocket> m_cancelledSockets = new List<NetMQSocket>();

		readonly SortedList<long, IList<NetMQTimer>> m_timers = new SortedList<long, IList<NetMQTimer>>();

		readonly CancellationTokenSource m_cancellationTokenSource;
		readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);
		private bool m_isStarted;

		private bool m_isDirty = false;

		public Poller()
		{
			PollTimeout = TimeSpan.FromSeconds(1);

			m_cancellationTokenSource = new CancellationTokenSource();
		}

		public TimeSpan PollTimeout { get; set; }

		public bool IsStarted { get { return m_isStarted; } }

		public void AddSocket(NetMQSocket socket)
		{
			if (m_sockets.Contains(socket) || m_newSockets.Contains(socket))
			{
				throw new ArgumentException("Socket already added to poller");
			}

			m_newSockets.Add(socket);

			socket.EventsChanged += OnSocketEventsChanged;

			m_isDirty = true;
		}

		public void RemoveSocket(NetMQSocket socket)
		{
			socket.EventsChanged -= OnSocketEventsChanged;

			if (m_newSockets.Contains(socket))
			{
				m_newSockets.Remove(socket);
			}
			else
			{
				m_cancelledSockets.Add(socket);

				m_isDirty = true;
			}
		}

		private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
		{
			// when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
			m_isDirty = true;
		}

		public void AddTimer(NetMQTimer timer)
		{
			if (!timer.Cancelled)
			{
				throw new ArgumentException("Timer already attached to a poller");
			}

			timer.Cancelled = false;
			timer.TimerChanged += OnTimerChanged;

			SetTimer(timer);
		}

		public void RemoveTimer(NetMQTimer timer)
		{
			timer.TimerChanged -= OnTimerChanged;
			timer.Cancelled = true;

			UnsetTimer(timer);
		}

		private void OnTimerChanged(object sender, NetMQTimerEventArgs e)
		{
			UnsetTimer(e.Timer);
			SetTimer(e.Timer);
		}

		private void SetTimer(NetMQTimer timer)
		{
			SetTimer(Clock.NowMs(), timer);
		}

		private void SetTimer(long current, NetMQTimer timer)
		{
			if (timer.Enable && !timer.Cancelled)
			{
				long expiration = current + (int)timer.Interval.TotalMilliseconds;

				if (!m_timers.ContainsKey(expiration))
				{
					m_timers.Add(expiration, new List<NetMQTimer>());
				}

				m_timers[expiration].Add(timer);
			}
		}

		private void UnsetTimer(NetMQTimer timer)
		{
			//  Complexity of this operation is O(n). We assume it is rarely used.
			var foundTimers = new Dictionary<long, NetMQTimer>();

			foreach (var pair in m_timers)
			{
				var foundTimer = pair.Value.FirstOrDefault(x => x == timer);

				if (foundTimer == null)
					continue;

				if (!foundTimers.ContainsKey(pair.Key))
				{
					foundTimers[pair.Key] = foundTimer;
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

		private int ExecuteTimers()
		{
			//  Fast track.
			if (!m_timers.Any())
				return 0;

			//  Get the current time.
			long current = Clock.NowMs();

			while (m_timers.Keys.Count > 0)
			{
				var key = m_timers.Keys[0];

				//  If we have to wait to execute the item, same will be true about
				//  all the following items (multimap is sorted). Thus we can stop
				//  checking the subsequent timers and return the time to wait for
				//  the next timer (at least 1ms).
				if (key > current)
				{
					return (int)(key - current);
				}

				var timers = m_timers[key];

				//  Remove it from the list of active timers.						
				m_timers.Remove(key);

				//  Trigger the timers. we make a copy of the list to avoid the enumerable was changed issue              
				foreach (var timer in timers)
				{
					// set the next run before the invokation to avoid to registeration
					SetTimer(current, timer);

					timer.InvokeElapsed(this);
				}
			}

			//  There are no more timers.
			return 0;
		}

		public void Start()
		{
			Thread.CurrentThread.Name = "NetMQPollerThread";

			m_isStoppedEvent.Reset();
			m_isStarted = true;
			try
			{

				// the sockets may have been created in another thread, to make sure we can fully use them we do full memory barried
				// at the begining of the loop
				Thread.MemoryBarrier();

				PollerPollItem[] items = new PollerPollItem[m_newSockets.Count];

				int count = 0;

				count = HandleNewSockets(count, ref items);

				count = HandleCancelledSockets(items, count);

				int maxTimeout = (int)PollTimeout.TotalMilliseconds;

				while (!m_cancellationTokenSource.IsCancellationRequested)
				{
					int timeout = ExecuteTimers();

					if (timeout == 0 || timeout > maxTimeout)
					{
						timeout = maxTimeout;
					}

					ZMQ.Poll(items, count, timeout);

					for (int index = 0; index < count; index++)
					{
						PollerPollItem item = items[index];
						item.NetMQSocket.InvokeEvents(this, item.ResultEvent);
					}

					if (m_isDirty)
					{
						for (int i = 0; i < count; i++)
						{
							items[i].Events = items[i].NetMQSocket.GetPollEvents();							
						}

						count = HandleNewSockets(count, ref items);

						count = HandleCancelledSockets(items, count);

						m_isDirty = false;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
			finally
			{
				m_isStoppedEvent.Set();
			}
		}

		private int HandleNewSockets(int count, ref PollerPollItem[] items)
		{
			if (m_newSockets.Any())
			{
				int itemsNewSize = count + m_newSockets.Count;

				if (itemsNewSize > items.Length)
				{
					PollerPollItem[] newArray = new PollerPollItem[itemsNewSize];
					Array.Copy(items, newArray, count);

					items = newArray;
				}

				foreach (NetMQSocket socket in m_newSockets)
				{
					m_sockets.Add(socket);
					items[count] = new PollerPollItem(socket, socket.GetPollEvents());
					count++;
				}

				m_newSockets.Clear();
			}

			return count;
		}

		private int HandleCancelledSockets(PollerPollItem[] items, int count)
		{
			foreach (NetMQSocket retiredSocket in m_cancelledSockets)
			{
				int currentIndex = 0;

				while (true)
				{
					if (items[currentIndex].NetMQSocket == retiredSocket)
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

				m_sockets.Remove(retiredSocket);
			}

			m_cancelledSockets.Clear();

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
