using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetMQ.Core;
using NetMQ.Core.Utils;

namespace NetMQ
{
    public static class NetMQPollerDesign
    {
        public static void Design()
        {
            using (var context = NetMQContext.Create())
            using (var socket1 = context.CreatePairSocket())
            using (var socket2 = context.CreatePairSocket())
            using (var socket3 = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IPv4))
            {
                var timer1 = new NetMQTimer(123);
                var timer2 = new NetMQTimer(123);

                Action<Socket> socketCallback = socket => { /* ... */ };

                using (var poller = new NetMQPoller(context)
                {
                    socket1, socket2, timer1, timer2, { socket3, socketCallback }
                })
                {
                    poller.Start();

                    // from another thread
                    poller.StopAsync();
                    poller.Stop();

                    // or simply
                    poller.Stop();

                    var task = new Task(() => socket1.Send(new byte[0]));
                    task.Start(poller);
                    task.Wait();
                }
            }
        }
    }

    public sealed class NetMQPoller : TaskScheduler, IEnumerable, IDisposable
    {
        private readonly IList<NetMQSocket> m_sockets = new List<NetMQSocket>();
        private readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
        private readonly IDictionary<Socket, Action<Socket>> m_pollinSockets = new Dictionary<Socket, Action<Socket>>();
        private readonly ManualResetEvent m_stoppedEvent = new ManualResetEvent(false);
        private readonly Selector m_selector = new Selector();

        private SelectItem[] m_pollSet;
        private NetMQSocket[] m_pollact;

        private volatile bool m_isStopRequested;
        private volatile int m_isDisposed;
        private volatile bool m_isPollSetDirty = true;

        #region Scheduling

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
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

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
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            m_tasksQueue.Enqueue(task);

            lock (m_schedulerPushSocket)
            {
                // awake the scheduler
                m_schedulerPushSocket.Send("");
            }
        }

        #endregion

        public NetMQPoller([NotNull] NetMQContext context)
        {
            PollTimeout = TimeSpan.FromSeconds(1);

            var address = string.Format("{0}://poller-{1}", Address.InProcProtocol, m_pollerId);

            m_schedulerPullSocket = context.CreatePullSocket();
            m_schedulerPullSocket.Options.Linger = TimeSpan.Zero;
            m_schedulerPullSocket.Bind(address);

            m_schedulerPullSocket.ReceiveReady += delegate
            {
                Debug.Assert(m_isDisposed == 0);
                Debug.Assert(IsStarted);

                // Dequeue the 'wake' command
                m_schedulerPullSocket.SkipFrame();

                Debug.Assert(!m_tasksQueue.IsEmpty);

                // Try to dequeue and execute all pending tasks
                Task task;
                while (m_tasksQueue.TryDequeue(out task))
                    TryExecuteTask(task);
            };

            m_sockets.Add(m_schedulerPullSocket);

            m_schedulerPushSocket = context.CreatePushSocket();
            m_schedulerPushSocket.Connect(address);
        }

        public bool IsStarted { get; private set; }

        /// <summary>
        /// Get or set the poll timeout.
        /// </summary>
        public TimeSpan PollTimeout { get; set; }

        #region Add / Remove

        public void Add([NotNull] ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            var task = new Task(() =>
            {
                if (m_sockets.Contains(socket.Socket))
                    return;

                m_sockets.Add(socket.Socket);

                socket.Socket.EventsChanged += OnSocketEventsChanged;
                m_isPollSetDirty = true;
            });
            task.Start(this);
        }

        public void Add([NotNull] NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            var task = new Task(() =>
            {
                m_timers.Add(timer);
            });
            task.Start(this);
        }

        public void Add([NotNull] Socket socket, [NotNull] Action<Socket> callback)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (callback == null)
                throw new ArgumentNullException("callback");
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            var task = new Task(() =>
            {
                if (m_pollinSockets.ContainsKey(socket))
                    return;
                m_pollinSockets.Add(socket, callback);
                m_isPollSetDirty = true;
            });
            task.Start(this);
        }

        public void Remove([NotNull] ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            var task = new Task(() =>
            {
                socket.Socket.EventsChanged -= OnSocketEventsChanged;
                m_sockets.Remove(socket.Socket);
                m_isPollSetDirty = true;
            });
            task.Start(this);
        }

        public void Remove([NotNull] NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            timer.When = -1;

            var task = new Task(() =>
            {
                m_timers.Remove(timer);
            });
            task.Start(this);
        }

        public void Remove([NotNull] Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");

            var task = new Task(() =>
            {
                m_pollinSockets.Remove(socket);
                m_isPollSetDirty = true;
            });
            task.Start(this);
        }

        #endregion

        #region Start / Stop

        public void StartAsync()
        {
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");
            if (IsStarted)
                throw new InvalidOperationException("NetMQPoller is already started");

            var thread = new Thread(Start) { Name = "NetMQPollerThread" };
            thread.Start();
        }

        public void Start()
        {
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");
            if (IsStarted)
                throw new InvalidOperationException("NetMQPoller is already started");

            var oldSynchronisationContext = SynchronizationContext.Current;

            SynchronizationContext.SetSynchronizationContext(new NetMQSynchronizationContext(this));

            m_isStopRequested = false;

            m_isSchedulerThread.Value = true;

            m_stoppedEvent.Reset();
            IsStarted = true;
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
                        // Don't pass anything less than 0 to sleep or risk an out of range exception or worse - infinity. Do not sleep on 0 from original code.
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
                    IsStarted = false;
                    m_isSchedulerThread.Value = false;
                    SynchronizationContext.SetSynchronizationContext(oldSynchronisationContext);
                    m_stoppedEvent.Set();
                }
            }
        }

        public void Stop()
        {
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");
            if (!IsStarted)
                throw new InvalidOperationException("Poller is not running");

            m_isStopRequested = true;
            m_stoppedEvent.WaitOne();
            Debug.Assert(!IsStarted);
        }

        public void StopAsync()
        {
            if (m_isDisposed != 0)
                throw new ObjectDisposedException("NetMQPoller");
            if (!IsStarted)
                throw new InvalidOperationException("Poller is not running");
            m_isStopRequested = true;
            Debug.Assert(!IsStarted);
        }

        #endregion

        private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
        {
            // when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
            m_isPollSetDirty = true;
        }

        private void RebuildPollset()
        {
            Debug.Assert(m_isSchedulerThread.Value);

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

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_isDisposed, 1, 0) == 1)
                return;

            // If this poller is already started, signal the polling thread to stop
            // and wait for it.
            if (IsStarted)
            {
                m_isStopRequested = true;
                m_stoppedEvent.WaitOne();
                Debug.Assert(!IsStarted);
            }

            m_stoppedEvent.Dispose();
            m_schedulerPullSocket.Dispose();
            m_schedulerPushSocket.Dispose();

            foreach (var socket in m_sockets)
                socket.EventsChanged -= OnSocketEventsChanged;
        }

        #endregion

        #region Synchronisation context

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

        #endregion
    }
}