using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Core.Utils;
#if !NET35
using NetMQ.Core;
using System.Collections.Concurrent;
using System.Threading.Tasks;
#endif

namespace NetMQ
{
    public sealed class NetMQPoller :
#if !NET35
        TaskScheduler,
#endif
        ISocketPollableCollection, IEnumerable, IDisposable
    {
        private readonly List<NetMQSocket> m_sockets = new List<NetMQSocket>();
        private readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
        private readonly Dictionary<Socket, Action<Socket>> m_pollinSockets = new Dictionary<Socket, Action<Socket>>();
        private readonly ManualResetEvent m_stoppedEvent = new ManualResetEvent(false);
        private readonly Selector m_selector = new Selector();

        private SelectItem[] m_pollSet;
        private NetMQSocket[] m_pollact;

        private volatile bool m_isStopRequested;
        private volatile bool m_isPollSetDirty = true;
        private int m_disposeState = (int)DisposeState.Undisposed;

        #region Scheduling

#if !NET35
        private static int s_nextPollerId;

        private readonly NetMQSocket m_schedulerPullSocket;
        private readonly NetMQSocket m_schedulerPushSocket;
        private readonly ThreadLocal<bool> m_isSchedulerThread = new ThreadLocal<bool>(() => false);
        private readonly ConcurrentQueue<Task> m_tasksQueue = new ConcurrentQueue<Task>();
        private readonly int m_pollerId = Interlocked.Increment(ref s_nextPollerId);

        /// <summary>
        /// Get whether the caller is running on the scheduler's thread.
        /// If <c>true</c>, the caller can execute tasks directly (inline).
        /// If <c>false</c>, the caller must start a <see cref="Task"/> on this scheduler.
        /// </summary>
        /// <remarks>
        /// This property enables avoiding the creation of a <see cref="Task"/> object and
        /// potential delays to its execution due to queueing. In most cases this is just
        /// an optimisation.
        /// </remarks>
        /// <example>
        /// <code>
        /// if (scheduler.CanExecuteTaskInline)
        /// {
        ///     socket.Send(...);
        /// }
        /// else
        /// {
        ///     var task = new Task(() => socket.Send(...));
        ///     task.Start(scheduler);
        /// }
        /// </code>
        /// </example>
        public bool CanExecuteTaskInline
        {
            get { return m_isSchedulerThread.Value; }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (task == null)
                throw new ArgumentNullException("task");
            CheckDisposed();

            return CanExecuteTaskInline && TryExecuteTask(task);
        }

        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // this is not supported, also it's only important for debug purposes and doesn't get called in real time.
            throw new NotSupportedException();
        }

        protected override void QueueTask(Task task)
        {
            if (task == null)
                throw new ArgumentNullException("task");
            CheckDisposed();

            m_tasksQueue.Enqueue(task);

            lock (m_schedulerPushSocket)
            {
                // awake the scheduler
                m_schedulerPushSocket.SendFrameEmpty();
            }
        }

        private void Run(Action action)
        {
            if (CanExecuteTaskInline)
                action();
            else
                new Task(action).Start(this);
        }
#else
        private void Run(Action action)
        {
            action();
        }
#endif

        #endregion

        public NetMQPoller()
        {
            PollTimeout = TimeSpan.FromSeconds(1);
#if !NET35
            var address = string.Format("{0}://netmqpoller-{1}", Address.InProcProtocol, m_pollerId);

            m_schedulerPullSocket = new NetMQ.Sockets.PullSocket();
            m_schedulerPullSocket.Options.Linger = TimeSpan.Zero;
            m_schedulerPullSocket.Bind(address);

            m_schedulerPullSocket.ReceiveReady += delegate
            {
                Debug.Assert(m_disposeState != (int)DisposeState.Disposed);
                Debug.Assert(IsRunning);

                // Dequeue the 'wake' command
                m_schedulerPullSocket.SkipFrame();

                // Try to dequeue and execute all pending tasks
                Task task;
                while (m_tasksQueue.TryDequeue(out task))
                    TryExecuteTask(task);
            };

            m_sockets.Add(m_schedulerPullSocket);

            m_schedulerPushSocket = new NetMQ.Sockets.PushSocket();
            m_schedulerPushSocket.Connect(address);
#endif
        }

        public bool IsRunning { get; private set; }

        public TimeSpan PollTimeout { get; set; }

        #region Add / Remove

        public void Add([NotNull] ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            CheckDisposed();

            Run(() =>
            {
                if (m_sockets.Contains(socket.Socket))
                    return;

                m_sockets.Add(socket.Socket);

                socket.Socket.EventsChanged += OnSocketEventsChanged;
                m_isPollSetDirty = true;
            });
        }

        public void Add([NotNull] NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            CheckDisposed();

            Run(() => m_timers.Add(timer));
        }

        public void Add([NotNull] Socket socket, [NotNull] Action<Socket> callback)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (callback == null)
                throw new ArgumentNullException("callback");
            CheckDisposed();

            Run(() =>
            {
                if (m_pollinSockets.ContainsKey(socket))
                    return;
                m_pollinSockets.Add(socket, callback);
                m_isPollSetDirty = true;
            });
        }

        public void Remove([NotNull] ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            CheckDisposed();

            Run(() =>
            {
                socket.Socket.EventsChanged -= OnSocketEventsChanged;
                m_sockets.Remove(socket.Socket);
                m_isPollSetDirty = true;
            });
        }

        public void Remove([NotNull] NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            CheckDisposed();

            timer.When = -1;

            Run(() => m_timers.Remove(timer));
        }

        public void Remove([NotNull] Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            CheckDisposed();

            Run(() =>
            {
                m_pollinSockets.Remove(socket);
                m_isPollSetDirty = true;
            });
        }

        #endregion

        #region Start / Stop

        public void RunAsync()
        {
            CheckDisposed();
            if (IsRunning)
                throw new InvalidOperationException("NetMQPoller is already running");

            var thread = new Thread(Run) { Name = "NetMQPollerThread" };
            thread.Start();
        }

        public void Run()
        {
            CheckDisposed();
            if (IsRunning)
                throw new InvalidOperationException("NetMQPoller is already started");

#if !NET35
            var oldSynchronisationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new NetMQSynchronizationContext(this));
            m_isSchedulerThread.Value = true;
#endif
            m_isStopRequested = false;

            m_stoppedEvent.Reset();
            IsRunning = true;
            try
            {
                // the sockets may have been created in another thread, to make sure we can fully use them we do full memory barrier
                // at the beginning of the loop
                Thread.MemoryBarrier();

                // Recalculate all timers now
                foreach (var timer in m_timers)
                {
                    if (timer.Enable)
                        timer.When = Clock.NowMs() + timer.Interval;
                }

                // Run until stop is requested
                while (!m_isStopRequested)
                {
                    if (m_isPollSetDirty)
                        RebuildPollset();

                    var pollStart = Clock.NowMs();

                    // Calculate tickless timer by adding the timeout to the current point-in-time.
                    long tickless = pollStart + (long)PollTimeout.TotalMilliseconds;

                    // Find the When-value of the earliest timer..
                    foreach (var timer in m_timers)
                    {
                        // If it is enabled but no When is set yet,
                        if (timer.When == -1 && timer.Enable)
                        {
                            // Set this timer's When to now plus it's Interval.
                            timer.When = pollStart + timer.Interval;
                        }

                        // If it has a When and that is earlier than the earliest found thus far,
                        if (timer.When != -1 && tickless > timer.When)
                        {
                            // save that value.
                            tickless = timer.When;
                        }
                    }

                    // Compute a timeout value - how many milliseconds from now that that earliest-timer will expire.
                    var timeout = tickless - pollStart;

                    // Use zero to indicate it has already expired.
                    if (timeout < 0)
                        timeout = 0;

                    var isItemAvailable = false;

                    if (m_pollSet.Length != 0)
                    {
                        isItemAvailable = m_selector.Select(m_pollSet, m_pollSet.Length, timeout);
                    }
                    else
                    {
                        if (timeout > 0)
                        {
                            //TODO: Do we really want to simply sleep and return, doing nothing during this interval?
                            //TODO: Should a large value be passed it will sleep for a month literally.
                            //      Solution should be different, but sleep is more natural here than in selector (timers are not selector concern).
                            Debug.Assert(timeout <= int.MaxValue);
                            Thread.Sleep((int)timeout);
                        }
                    }

                    // Get the expected end time in case we time out. This looks redundant but, unfortunately,
                    // it happens that Poll takes slightly less than the requested time and 'Clock.NowMs() >= timer.When'
                    // may not true, even if it is supposed to be. In other words, even when Poll times out, it happens
                    // that 'Clock.NowMs() < pollStart + timeout'
                    var expectedPollEnd = !isItemAvailable ? pollStart + timeout : -1L;

                    // that way we make sure we can continue the loop if new timers are added.
                    // timers cannot be removed
                    foreach (var timer in m_timers)
                    {
                        if ((Clock.NowMs() >= timer.When || expectedPollEnd >= timer.When) && timer.When != -1)
                        {
                            timer.InvokeElapsed(this);

                            if (timer.Enable)
                                timer.When = Clock.NowMs() + timer.Interval;
                        }
                    }

                    for (var i = 0; i < m_pollSet.Length; i++)
                    {
                        SelectItem item = m_pollSet[i];

                        if (item.Socket != null)
                        {
                            NetMQSocket socket = m_pollact[i];

                            if (item.ResultEvent.HasError())
                            {
                                if (++socket.Errors > 1)
                                {
                                    Remove(socket);
                                    item.ResultEvent = PollEvents.None;
                                }
                            }
                            else
                            {
                                socket.Errors = 0;
                            }

                            if (item.ResultEvent != PollEvents.None)
                                socket.InvokeEvents(this, item.ResultEvent);
                        }
                        else if (item.ResultEvent.HasError() || item.ResultEvent.HasIn())
                        {
                            Action<Socket> action;
                            if (m_pollinSockets.TryGetValue(item.FileDescriptor, out action))
                                action(item.FileDescriptor);
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    foreach (var socket in m_sockets.ToList())
                        Remove(socket);
                }
                finally
                {
                    IsRunning = false;
#if !NET35
                    m_isSchedulerThread.Value = false;
                    SynchronizationContext.SetSynchronizationContext(oldSynchronisationContext);
#endif
                    m_stoppedEvent.Set();
                }
            }
        }

        public void Stop()
        {
            CheckDisposed();
            if (!IsRunning)
                throw new InvalidOperationException("Poller is not running");

            m_isStopRequested = true;
            m_stoppedEvent.WaitOne();
            Debug.Assert(!IsRunning);
        }

        public void StopAsync()
        {
            CheckDisposed();
            if (!IsRunning)
                throw new InvalidOperationException("Poller is not running");
            m_isStopRequested = true;
            Debug.Assert(!IsRunning);
        }

        #endregion

        private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
        {
            // when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
            m_isPollSetDirty = true;
        }

        private void RebuildPollset()
        {
#if !NET35
            Debug.Assert(m_isSchedulerThread.Value);
#endif

            // Recreate the m_pollSet and m_pollact arrays.
            m_pollSet = new SelectItem[m_sockets.Count + m_pollinSockets.Count];
            m_pollact = new NetMQSocket[m_sockets.Count];

            // For each socket in m_sockets,
            // put a corresponding SelectItem into the m_pollSet array and a reference to the socket itself into the m_pollact array.
            uint index = 0;

            foreach (var socket in m_sockets)
            {
                m_pollSet[index] = new SelectItem(socket.SocketHandle, socket.GetPollEvents());
                m_pollact[index] = socket;
                index++;
            }

            foreach (var socket in m_pollinSockets.Keys)
            {
                m_pollSet[index] = new SelectItem(socket, PollEvents.PollError | PollEvents.PollIn);
                index++;
            }

            // Mark this as NOT having any fresh events to attend to, as yet.
            m_isPollSetDirty = false;
        }

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            yield break;
        }

        #endregion

        #region IDisposable

        private enum DisposeState
        {
            Undisposed = 0,
            Disposing = 1,
            Disposed = 2
        }

        private void CheckDisposed()
        {
            if (m_disposeState == (int)DisposeState.Disposed)
                throw new ObjectDisposedException("NetMQPoller");
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_disposeState, (int)DisposeState.Disposing, 0) == (int)DisposeState.Disposing)
                return;

            // If this poller is already started, signal the polling thread to stop
            // and wait for it.
            if (IsRunning)
            {
                m_isStopRequested = true;
                m_stoppedEvent.WaitOne();
                Debug.Assert(!IsRunning);
            }

            m_stoppedEvent.Close();

#if !NET35
            m_schedulerPullSocket.Dispose();
            m_schedulerPushSocket.Dispose();
#endif

            foreach (var socket in m_sockets)
                socket.EventsChanged -= OnSocketEventsChanged;

            m_disposeState = (int)DisposeState.Disposed;
        }

        #endregion

        #region Synchronisation context

#if !NET35
        private sealed class NetMQSynchronizationContext : SynchronizationContext
        {
            private readonly NetMQPoller m_poller;

            public NetMQSynchronizationContext(NetMQPoller poller)
            {
                m_poller = poller;
            }

            /// <summary>Dispatches an asynchronous message to a synchronization context.</summary>
            public override void Post(SendOrPostCallback d, object state)
            {
                var task = new Task(() => d(state));
                task.Start(m_poller);
            }

            /// <summary>Dispatches a synchronous message to a synchronization context.</summary>
            public override void Send(SendOrPostCallback d, object state)
            {
                var task = new Task(() => d(state));
                task.Start(m_poller);
                task.Wait();
            }
        }
#endif

        #endregion
    }
}