using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Core.Utils;

namespace NetMQ
{
    /// <summary>
    /// The Poller class provides for managing a set of one or more sockets and being alerted when one of them has a message
    /// ready.
    /// </summary>
    [Obsolete("Use NetMQPoller instead")]
    public class Poller : INetMQPoller, ISocketPollableCollection, IDisposable
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
        /// Switch indicate if the poller is on or off.
        /// </summary>
        private readonly Switch m_switch = new Switch(false);

        /// <summary>
        /// When true, this indicates that there may be fresh socket-events to be polled-for.
        /// This is set true whenever a EventsChanged event is received from any of the contained sockets.
        /// </summary>
        private bool m_isDirty = true;

        /// <summary>
        /// This flag is used to mark this poller as disposed when the Dispose method is called,
        /// to guard against further operations on this poller-object.
        /// </summary>
        private bool m_disposed;

        private readonly Selector m_selector = new Selector();

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
        /// <exception cref="ArgumentNullException">sockets must not be null.</exception>
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
        /// <exception cref="ArgumentNullException">timers must not be null.</exception>
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

        /// <summary>
        /// Get whether the polling has started.
        /// This is set true at the beginning of the Start method, and cleared in the Stop method.
        /// </summary>
        public bool IsStarted
        {
            get { return m_switch.Status; }
        }

        /// <summary>
        /// Get or set the poll timeout in milliseconds.
        /// </summary>
        public int PollTimeout { get; set; }

        /// <summary>
        /// Add the given socket and Action to this Poller's collection of socket/actions.
        /// </summary>
        /// <param name="socket">the Socket to add</param>
        /// <param name="callback">the Action to add</param>
        /// <exception cref="ArgumentNullException">socket and callback must not be null.</exception>
        /// <exception cref="ArgumentException">The socket must not have already been added to this poller.</exception>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
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
        /// <exception cref="ArgumentNullException">socket must not be null.</exception>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
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
        /// <exception cref="ArgumentNullException">socket must not be null.</exception>
        /// <exception cref="ArgumentException">socket must not have already been added to this poller.</exception>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
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
        /// <exception cref="ArgumentNullException">socket must not be null.</exception>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
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
        ///  Delete all sockets from this Poller's list.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
        public void RemoveAllSockets()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            foreach(var socket in m_sockets)
            {
                RemoveSocket(socket);
            }
        }

        /// <summary>
        /// Add the given timer to this Poller's list.
        /// </summary>
        /// <param name="timer">the NetMQTimer to add to the list</param>
        /// <exception cref="ArgumentNullException">timer must not be null.</exception>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
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
        /// <exception cref="ArgumentNullException">timer must not be null.</exception>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
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
        /// Remove all timers from this Poller's list.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
        public void RemoveAllTimers()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            foreach(NetMQTimer timer in m_timers)
            {
                RemoveTimer(timer);
            }
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
            m_switch.WaitForOn();
        }

        /// <summary>
        /// Poll till Cancel or CancelAndJoin is called. This is a blocking method.
        /// </summary>
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

            // Recreate the m_pollset and m_pollact arrays.
            m_pollset = new SelectItem[m_pollSize];
            m_pollact = new NetMQSocket[m_sockets.Count];

            // For each socket in m_sockets,
            // put a corresponding SelectItem into the m_pollset array and a reference to the socket itself into the m_pollact array.
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

            // Mark this as NOT having any fresh events to attend to, as yet.
            m_isDirty = false;
        }

        /// <summary>
        /// Return the soonest timeout value of all the timers in the list, or zero to indicate any of them have already elapsed.
        /// The timeout is in milliseconds from now.
        /// </summary>
        /// <returns>the number of milliseconds before the first timer expires, or zero if one already has</returns>
        private int TicklessTimer()
        {
            // Calculate tickless timer,
            // by adding the timeout to the current point-in-time.
            long tickless = Clock.NowMs() + PollTimeout;

            // Find the When-value of the earliest timer..
            foreach (var timer in m_timers)
            {
                // If it is enabled but no When is set yet,
                if (timer.When == -1 && timer.Enable)
                {
                    // Set this timer's When to now plus it's Interval.
                    timer.When = timer.Interval + Clock.NowMs();
                }
                // If it has a When and that is earlier than the earliest found thus far,
                if (timer.When != -1 && tickless > timer.When)
                {
                    // save that value.
                    tickless = timer.When;
                }
            }
            // Compute a timeout value - how many milliseconds from now that that earliest-timer will expire.
            var timeout = (int)(tickless - Clock.NowMs());
            // Use zero to indicate it has already expired.
            if (timeout < 0)
            {
                timeout = 0;
            }
            // Return that timeout value.
            return timeout;
        }

        /// <summary>
        /// Poll as long as the given Func evaluates to true.
        /// </summary>
        /// <param name="condition">a Func that returns a boolean value, to evaluate on each poll-iteration to decide when to exit the loop</param>
        /// <exception cref="ObjectDisposedException">This poller must not have already been disposed.</exception>
        /// <exception cref="InvalidOperationException">This poller must not have already been started.</exception>
        private void PollWhile([NotNull, InstantHandle] Func<bool> condition)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            if (m_switch.Status)
            {
                throw new InvalidOperationException("Poller is started");
            }

            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "NetMQPollerThread";

            m_switch.SwitchOn();
            try
            {
                // the sockets may have been created in another thread, to make sure we can fully use them we do full memory barrier
                // at the beginning of the loop
                Thread.MemoryBarrier();

                // Recalculate all timers now
                foreach (var timer in m_timers)
                {
                    if (timer.Enable)
                    {
                        timer.When = Clock.NowMs() + timer.Interval;
                    }
                }

                // Do this until the given Func evaluates to false...
                while (condition())
                {
                    if (m_isDirty)
                    {
                        RebuildPollset();
                    }

                    var pollStart = Clock.NowMs();
                    var timeout = TicklessTimer();

                    var isItemsAvailable = false;

                    if (m_pollSize > 0)
                    {
                        isItemsAvailable = m_selector.Select(m_pollset, m_pollSize, timeout);
                    }
                    else
                    {
                        // Don't pass anything less than 0 to sleep or risk an out of range exception or worse - infinity. Do not sleep on 0 from orginal code.
                        if (timeout > 0)
                        {
                            //TODO: Do we really want to simply sleep and return, doing nothing during this interval?
                            //TODO: Should a large value be passed it will sleep for a month literaly.
                            //      Solution should be different, but sleep is more natural here than in selector (timers are not selector concern).
                            Thread.Sleep(timeout);
                        }
                    }

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

                            if (item.ResultEvent.HasError())
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
                            if (item.ResultEvent.HasError() || item.ResultEvent.HasIn())
                            {
                                Action<Socket> action;

                                if (m_pollinSockets.TryGetValue(item.FileDescriptor, out action))
                                {
                                    action(item.FileDescriptor);
                                }
                            }
                        }
                    }

                    if (m_zombies.Count > 0)
                    {
                        // Now handle any timer zombies
                        // This is going to be slow if we have many zombies
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
                    m_switch.SwitchOff();
                }
            }
        }

        /// <summary>
        /// Signal this poller to stop, and return immediately if waitForCloseToComplete is false,
        /// block until the poller has actually stopped if waitForCloseToComplete is true.
        /// </summary>
        /// <param name="waitForCloseToComplete">block until the poller has actually stopped</param>
        /// <exception cref="ObjectDisposedException">if this poller has already been disposed</exception>
        /// <exception cref="InvalidOperationException">if this poller has not been started</exception>
        private void Cancel(bool waitForCloseToComplete)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is already disposed");
            }

            if (m_switch.Status)
            {
                Interlocked.Exchange(ref m_cancel, 1);

                if (waitForCloseToComplete)
                {
                    m_switch.WaitForOff();
                }
            }
            else
            {
                throw new InvalidOperationException("Poller is unstarted");
            }
        }

        /// <summary>
        /// Gets whether <paramref name="socket"/> has been added to this <see cref="Poller"/>.
        /// </summary>
        public bool ContainsSocket(NetMQSocket socket)
        {
            return m_sockets.Contains(socket);
        }

        #region Dispose

        /// <summary>
        /// Perform any freeing, releasing or resetting of contained resources.
        /// If this poller is already started, signal the polling thread to stop - and block to wait for it.
        /// </summary>
        /// <remarks>
        /// Calling this again after the first time, does nothing.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Perform any freeing, releasing or resetting of contained resources.
        /// If this poller is already started, signal the polling thread to stop - and block to wait for it.
        /// </summary>
        /// <param name="disposing">true if releasing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (!m_disposed)
            {
                // If this poller is already started, signal the polling thread to stop
                // and wait for it.
                if (m_switch.Status)
                    Cancel(true);

                m_disposed = true;                
            }
        }

        #endregion

        #region ISocketPollableCollection members

        // NOTE this interface provides an abstraction over the legacy Poller and newer NetMQPoller classes for use in NetMQMonitor

        void ISocketPollableCollection.Add(ISocketPollable socket)
        {
            AddSocket(socket);
        }

        void ISocketPollableCollection.Remove(ISocketPollable socket)
        {
            RemoveSocket(socket);
        }

        #endregion

        #region INetMQPoller members

        void INetMQPoller.Run()
        {
            PollTillCancelled();
        }

        void INetMQPoller.RunAsync()
        {
            PollTillCancelledNonBlocking();
        }

        void INetMQPoller.Stop()
        {
            CancelAndJoin();
        }

        void INetMQPoller.StopAsync()
        {
            Cancel();
        }

        void INetMQPoller.Add(ISocketPollable socket)
        {
            AddSocket(socket);
        }

        bool INetMQPoller.IsRunning
        {
            get { return IsStarted; }
        }

        #endregion
    }
}
