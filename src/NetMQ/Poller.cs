using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.zmq;
using NetMQ.zmq.Utils;

namespace NetMQ
{
    public class Poller : IDisposable
    {
        /// <summary>
        /// This is the list of sockets that this Poller is listening to.
        /// </summary>
        private readonly IList<NetMQSocket> m_sockets = new List<NetMQSocket>();

        /// <summary>
        /// A Dictionary of entries each of which associates a socket with an action.
        /// </summary>
        private readonly IDictionary<Socket, Action<Socket>> m_pollinSockets = new Dictionary<Socket, Action<Socket>>();

        private SelectItem[] m_pollset;

        /// <summary>
        /// An array of sockets that are active.
        /// </summary>
        private NetMQSocket[] m_pollact;

        private int m_pollSize;

        /// <summary>
        /// This is the list of NetMQTimers.
        /// </summary>
        private readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();

        /// <summary>
        /// This is used to hold the list of expired timers.
        /// </summary>
        private readonly List<NetMQTimer> m_zombies = new List<NetMQTimer>();

        /// <summary>
        /// Flag used to signal completion of the loop.
        /// </summary>
        private int m_cancel;

        /// <summary>
        /// This ManualResetEvent is used for blocking until this Poller is actually stopped.
        /// </summary>
        private readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);

        /// <summary>
        /// This flag indicates whether this Poller has started.
        /// </summary>
        private bool m_isStarted;

        private bool m_isDirty = true;
        private bool m_disposed;

        private readonly Selector m_selector = new Selector();

        #region constructors
        /// <summary>
        /// Create a new Poller object, with a default PollTimeout of 1 second.
        /// </summary>
        public Poller()
        {
            PollTimeout = 1000;

            m_cancel = 0;
        }

        /// <summary>
        /// Create a new Poller object with an array of sockets.
        /// </summary>
        /// <param name="sockets">an array of ISocketPollables to poll</param>
        public Poller([NotNull] [ItemNotNull] params ISocketPollable[] sockets)
            : this()
        {
            if (sockets == null)
            {
                throw new ArgumentNullException("sockets");
            }

            foreach (var socket in sockets)
            {
                AddSocket(socket);
            }
        }

        /// <summary>
        /// Create a new Poller object with the given array of timers.
        /// </summary>
        /// <param name="timers">an array of NetMQTimers (must not be null) to be incorporated into this Poller</param>
        public Poller([NotNull] [ItemNotNull] params NetMQTimer[] timers)
            : this()
        {
            if (timers == null)
            {
                throw new ArgumentNullException("timers");
            }

            foreach (var timer in timers)
            {
                AddTimer(timer);
            }
        }
        #endregion constructors

        #region Dispose

        public void Dispose()
        {
            if (!m_disposed)
            {
                if (m_isStarted)
                    Cancel(true);

                m_disposed = true;
                m_isStoppedEvent.Close();
            }
        }

        #endregion

        #region public properties

        /// <summary>
        /// Get whether the polling has started.
        /// This is set true at the beginning of the Start method, and cleared in the Stop method.
        /// </summary>
        public bool IsStarted
        {
            get { return m_isStarted; }
        }

        /// <summary>
        /// Get or set the poll timeout in milliseconds.
        /// </summary>
        public int PollTimeout { get; set; }

        #endregion public properties

        #region public methods

        /// <summary>
        /// Add the given socket and Action to this Poller's collection of socket/actions.
        /// </summary>
        /// <param name="socket">the Socket to add</param>
        /// <param name="callback">the Action to add</param>
        public void AddPollInSocket([NotNull] Socket socket, [NotNull] Action<Socket> callback)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            if (m_pollinSockets.ContainsKey(socket))
            {
                throw new ArgumentException("Socket already added to poller");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            m_pollinSockets.Add(socket, callback);

            m_isDirty = true;
        }

        /// <summary>
        /// Delete the given socket from this Poller's collection of socket/actions.
        /// </summary>
        /// <param name="socket">the Socket to remove</param>
        public void RemovePollInSocket([NotNull] Socket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            m_pollinSockets.Remove(socket);

            m_isDirty = true;
        }

        /// <summary>
        /// Add the given socket to this Poller's list.
        /// </summary>
        /// <param name="socket">the ISocketPollable to add to the list</param>
        public void AddSocket([NotNull] ISocketPollable socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            if (m_sockets.Contains(socket.Socket))
            {
                throw new ArgumentException("Socket already added to poller");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            m_sockets.Add(socket.Socket);

            socket.Socket.EventsChanged += OnSocketEventsChanged;

            m_isDirty = true;
        }

        /// <summary>
        /// Delete the given socket from this Poller's list.
        /// </summary>
        /// <param name="socket">the ISocketPollable to remove from the list</param>
        public void RemoveSocket([NotNull] ISocketPollable socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            socket.Socket.EventsChanged -= OnSocketEventsChanged;

            m_sockets.Remove(socket.Socket);
            m_isDirty = true;
        }

        /// <summary>
        /// Add the given timer to this Poller's list.
        /// </summary>
        /// <param name="timer">the NetMQTimer to add to the list</param>
        public void AddTimer([NotNull] NetMQTimer timer)
        {
            if (timer == null)
            {
                throw new ArgumentNullException("timer");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            m_timers.Add(timer);
        }

        /// <summary>
        /// Delete the given timer from this Poller's list.
        /// </summary>
        /// <param name="timer">the NetMQTimer to remove from the list</param>
        public void RemoveTimer([NotNull] NetMQTimer timer)
        {
            if (timer == null)
            {
                throw new ArgumentNullException("timer");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            timer.When = -1;
            m_zombies.Add(timer);
        }

        /// <summary>
        /// Cancel the poller job when PollTillCancelled is called
        /// </summary>
        public void Cancel()
        {
            Cancel(false);
        }

        /// <summary>
        /// Cancel the poller job when PollTillCancelled is called and wait for the PollTillCancelled to complete
        /// </summary>
        public void CancelAndJoin()
        {
            Cancel(true);
        }

        /// <summary>
        /// Poll one time.
        /// </summary>
        public void PollOnce()
        {
            int timesToPoll = 1;
            PollWhile(() => timesToPoll-- > 0);
        }

        /// <summary>
        /// Poll till Cancel or CancelAndJoin is called. This is a blocking method.
        /// </summary>
        public void PollTillCancelled()
        {
            PollWhile(() => m_cancel == 0);
        }

        /// <summary>
        /// The non blocking version of PollTillCancelled, starting the PollTillCancelled on new thread. 
        /// Will poll till Cancel or CancelAndJoin is called. This method is not blocking.
        /// </summary>
        public void PollTillCancelledNonBlocking()
        {
            var thread = new Thread(PollTillCancelled);
            thread.Start();
        }

        [Obsolete("Use PollTillCancelled instead")]
        public void Start()
        {
            PollTillCancelled();
        }

        /// <summary>
        /// Stop the poller job. It may take a while until the poller is fully stopped.
        /// If it doesn't stop within 20 seconds, it times-out anyway and returns.
        /// </summary>
        /// <param name="waitForCloseToComplete">if true, this method will block until the poller is fully stopped</param>
        [Obsolete("Use Cancel(if your argument was false) or CancelAndJoin (if your argument was true)")]
        public void Stop(bool waitForCloseToComplete)
        {
            Cancel(waitForCloseToComplete);
        }

        /// <summary>
        /// Stop the poller job. This returns after waiting for that thread to stop.
        /// This is equivalent to calling CancelAndJoin.
        /// </summary>
        [Obsolete("Use CancelAndJoin")]
        public void Stop()
        {
            Cancel(true);
        }

        #endregion public methods

        #region internal implementation

        /// <summary>
        /// Handle the EventsChanged event of any of the sockets contained within this Poller,
        /// by marking this poller as "dirty" in order to reset the poll events.
        /// </summary>
        /// <param name="sender">the object that was the source of the event</param>
        /// <param name="e">the NetMQSocketEventArgs associated with the event</param>
        private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
        {
            // when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
            m_isDirty = true;
        }

        private void RebuildPollset()
        {
            m_pollSize = m_sockets.Count + m_pollinSockets.Count;

            m_pollset = new SelectItem[m_pollSize];
            m_pollact = new NetMQSocket[m_sockets.Count];

            uint itemNbr = 0;
            foreach (var socket in m_sockets)
            {
                m_pollset[itemNbr] = new SelectItem(socket.SocketHandle, socket.GetPollEvents());
                m_pollact[itemNbr] = socket;
                itemNbr++;
            }

            foreach (var socket in m_pollinSockets)
            {
                m_pollset[itemNbr] = new SelectItem(socket.Key, PollEvents.PollError | PollEvents.PollIn);
                itemNbr++;
            }

            m_isDirty = false;
        }

        private int TicklessTimer()
        {
            //  Calculate tickless timer
            long tickless = Clock.NowMs() + PollTimeout;

            foreach (var timer in m_timers)
            {
                //  Find earliest timer
                if (timer.When == -1 && timer.Enable)
                {
                    timer.When = timer.Interval + Clock.NowMs();
                }

                if (timer.When != -1 && tickless > timer.When)
                {
                    tickless = timer.When;
                }
            }

            var timeout = (int)(tickless - Clock.NowMs());
            if (timeout < 0)
            {
                timeout = 0;
            }

            return timeout;
        }

        /// <summary>
        /// Poll as long as the given Func evaluates to true.
        /// </summary>
        /// <param name="condition">a Func that returns a boolean value, to evaluate on each iteration</param>
        private void PollWhile([NotNull,InstantHandle] Func<bool> condition)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            if (m_isStarted)
            {
                throw new InvalidOperationException("Poller is started");
            }

            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "NetMQPollerThread";

            m_isStoppedEvent.Reset();
            m_isStarted = true;
            try
            {
                // the sockets may have been created in another thread, to make sure we can fully use them we do full memory barrier
                // at the beginning of the loop
                Thread.MemoryBarrier();

                //  Recalculate all timers now
                foreach (var timer in m_timers)
                {
                    if (timer.Enable)
                    {
                        timer.When = Clock.NowMs() + timer.Interval;
                    }
                }

                while (condition())
                {
                    if (m_isDirty)
                    {
                        RebuildPollset();
                    }

                    var pollStart = Clock.NowMs();
                    var timeout = TicklessTimer();

                    var isItemsAvailable = m_selector.Select(m_pollset, m_pollSize, timeout);

                    // Get the expected end time in case we time out. This looks redundant but, unfortunately,
                    // it happens that Poll takes slightly less than the requested time and 'Clock.NowMs() >= timer.When'
                    // may not true, even if it is supposed to be. In other words, even when Poll times out, it happens
                    // that 'Clock.NowMs() < pollStart + timeout'
                    var expectedPollEnd = !isItemsAvailable ? pollStart + timeout : -1;

                    // that way we make sure we can continue the loop if new timers are added.
                    // timers cannot be removed
                    int timersCount = m_timers.Count;
                    for (int i = 0; i < timersCount; i++)
                    {
                        var timer = m_timers[i];

                        if ((Clock.NowMs() >= timer.When || expectedPollEnd >= timer.When) && timer.When != -1)
                        {
                            timer.InvokeElapsed(this);

                            if (timer.Enable)
                            {
                                timer.When = timer.Interval + Clock.NowMs();
                            }
                        }
                    }

                    for (int itemNbr = 0; itemNbr < m_pollSize; itemNbr++)
                    {
                        SelectItem item = m_pollset[itemNbr];

                        if (item.Socket != null)
                        {
                            NetMQSocket socket = m_pollact[itemNbr];

                            if (item.ResultEvent.HasFlag(PollEvents.PollError))
                            {
                                socket.Errors++;

                                if (socket.Errors > 1)
                                {
                                    RemoveSocket(socket);
                                    item.ResultEvent = PollEvents.None;
                                }
                            }
                            else
                            {
                                socket.Errors = 0;
                            }

                            if (item.ResultEvent != PollEvents.None)
                            {
                                socket.InvokeEvents(this, item.ResultEvent);
                            }
                        }
                        else
                        {
                            if (item.ResultEvent.HasFlag(PollEvents.PollError) || item.ResultEvent.HasFlag(PollEvents.PollIn))
                            {
                                Action<Socket> action;

                                if (m_pollinSockets.TryGetValue(item.FileDescriptor, out action))
                                {
                                    action(item.FileDescriptor);
                                }
                            }
                        }
                    }

                    if (m_zombies.Any())
                    {
                        //  Now handle any timer zombies
                        //  This is going to be slow if we have many zombies
                        foreach (var netMQTimer in m_zombies)
                        {
                            m_timers.Remove(netMQTimer);
                        }

                        m_zombies.Clear();
                    }
                }
            }
            finally
            {
                try
                {
                    foreach (var socket in m_sockets.ToList())
                    {
                        RemoveSocket(socket);
                    }
                }
                finally
                {
                    m_isStarted = false;
                    m_isStoppedEvent.Set();
                }
            }
        }

        private void Cancel(bool waitForCloseToComplete)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is already disposed");
            }

            if (m_isStarted)
            {
                Interlocked.Exchange(ref m_cancel, 1);

                if (waitForCloseToComplete)
                {
                    m_isStoppedEvent.WaitOne();
                }

                m_isStarted = false;
            }
            else
            {
                throw new InvalidOperationException("Poller is unstarted");
            }
        }

        #endregion internal implementation
    }
}
