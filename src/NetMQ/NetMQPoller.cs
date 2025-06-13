using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using NetMQ.Core.Utils;
#if !NET35
using System.Threading.Tasks;
#endif
#if NET40
using System.ComponentModel;
#endif

using Switch = NetMQ.Core.Utils.Switch;

namespace NetMQ
{
    /// <summary>
    /// Enable polling on multiple NetMQSockets
    /// </summary>
    public sealed class NetMQPoller :
#if !NET35
        TaskScheduler,
#endif
#if NET40
        ISynchronizeInvoke,
#endif
#pragma warning disable 618
        INetMQPoller, ISocketPollableCollection, IEnumerable, IDisposable
#pragma warning restore 618
    {
        private readonly List<NetMQSocket> m_sockets = new List<NetMQSocket>();
        private readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
        private readonly Dictionary<Socket, Action<Socket>> m_pollinSockets = new Dictionary<Socket, Action<Socket>>();
        private readonly Switch m_switch = new Switch(false);
        private readonly NetMQSelector m_netMqSelector = new NetMQSelector();
        private readonly StopSignaler m_stopSignaler = new StopSignaler();

        private NetMQSelector.Item[]? m_pollSet;
		private NetMQSocket[]? m_pollact;

		private volatile bool m_isPollSetDirty = true;
        private int m_disposeState = (int)DisposeState.Undisposed;

#if NET35
        private Thread m_pollerThread;
#endif

        #region Scheduling

#if !NET35
        private readonly NetMQQueue<Task> m_tasksQueue = new NetMQQueue<Task>();
        private readonly ThreadLocal<bool> m_isSchedulerThread = new ThreadLocal<bool>(() => false);

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
        public bool CanExecuteTaskInline => m_isSchedulerThread.Value;

        /// <inheritdoc />
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (IsDisposed)
                return false;

            return CanExecuteTaskInline && TryExecuteTask(task);
        }

        /// <summary>
        /// Returns 1, as <see cref="NetMQPoller"/> runs a single thread and all tasks must execute on that thread.
        /// </summary>
        public override int MaximumConcurrencyLevel => 1;

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // this is not supported, also it's only important for debug purposes and doesn't get called in real time.
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override void QueueTask(Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            CheckDisposed();

            // We are not allowing new tasks will disposing
            if (m_disposeState == (int)DisposeState.Disposing)
                throw new ObjectDisposedException("NetMQPoller");

            m_tasksQueue.Enqueue(task);
        }

        /// <summary>
        /// Run an action on the Poller thread
        /// </summary>
        /// <param name="action">The action to run</param>
        public void Run(Action action)
        {
            if (!IsRunning || CanExecuteTaskInline)
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

        /// <summary>
        /// Create a new NetMQPoller
        /// </summary>
        public NetMQPoller()
        {
            m_sockets.Add(((ISocketPollable)m_stopSignaler).Socket);

#if !NET35

            m_tasksQueue.ReceiveReady += delegate
            {
                Debug.Assert(m_disposeState != (int)DisposeState.Disposed);
                Debug.Assert(IsRunning);

                // Try to dequeue and execute all pending tasks
                while (m_tasksQueue.TryDequeue(out Task? task, TimeSpan.Zero))
                    TryExecuteTask(task);
            };

            m_sockets.Add(((ISocketPollable)m_tasksQueue).Socket);
#endif
        }

        /// <summary>
        /// Get whether this object is currently polling its sockets and timers.
        /// </summary>
        public bool IsRunning => m_switch.Status;

        /// <summary>
        /// Get whether this object is currently disposed
        /// </summary>
        public bool IsDisposed => m_disposeState == (int)DisposeState.Disposed;

#if NET35
        private bool IsPollerThread => ReferenceEquals(m_pollerThread, Thread.CurrentThread);
#else
        private bool IsPollerThread => m_isSchedulerThread.Value;
#endif

        #region Add / Remove

        /// <summary>
        /// Add a socket to the poller
        /// </summary>
        /// <param name="socket">Socket to add to the poller</param>
        public void Add(ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            if (socket.IsDisposed)
                throw new ArgumentException("Must not be disposed.", nameof(socket));
            CheckDisposed();

            Run(() =>
            {
                // Ensure the socket wasn't disposed while this code was waiting to be run on the poller thread
                if (socket.IsDisposed)
                    throw new InvalidOperationException(
                        $"{nameof(NetMQPoller)}.{nameof(Add)} was called from a non-poller thread, " +
                        "so ran asynchronously. " +
                        $"The {socket.GetType().Name} being added was disposed while the addition " +
                        "operation waited to start on the poller thread. You must remove a socket " +
                        "before disposing it, or dispose the poller first.");

                if (m_sockets.Contains(socket.Socket))
                    return;

                m_sockets.Add(socket.Socket);

                socket.Socket.EventsChanged += OnSocketEventsChanged;
                m_isPollSetDirty = true;
            });
        }

        /// <summary>
        /// Add the timer to the Poller, the timer will be invoked on the poller thread when interval elapsed.
        /// </summary>
        /// <param name="timer">The timer to add to poller</param>
        /// <exception cref="ArgumentNullException">If timer is null</exception>
        public void Add(NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException(nameof(timer));
            CheckDisposed();

            Run(() => m_timers.Add(timer));
        }

        /// <summary>
        /// Add a regular .Net Socket to the poller.
        /// The callback will be invoked when the data is ready to be read from the socket
        /// </summary>
        /// <param name="socket">The socket to poll on</param>
        /// <param name="callback">The callback to invoke when the socket is ready</param>
        /// <exception cref="ArgumentNullException">If callback or socket are null</exception>
        public void Add(Socket socket, Action<Socket> callback)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            CheckDisposed();

            Run(() =>
            {
                if (m_pollinSockets.ContainsKey(socket))
                    return;
                m_pollinSockets.Add(socket, callback);
                m_isPollSetDirty = true;
            });
        }

        /// <summary>
        /// Remove a socket from the poller
        /// </summary>
        /// <param name="socket">The socket to be removed</param>
        /// <exception cref="ArgumentNullException">If socket is null</exception>
        /// <exception cref="ArgumentException">If socket is already disposed</exception>
        /// <exception cref="InvalidOperationException">If socket is getting disposed during the operation</exception>
        public void Remove(ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            if (socket.IsDisposed)
                throw new ArgumentException("Must not be disposed.", nameof(socket));
            CheckDisposed();

            Run(() =>
            {
                // Ensure the socket wasn't disposed while this code was waiting to be run on the poller thread
                if (socket.IsDisposed)
                    throw new InvalidOperationException(
                        $"{nameof(NetMQPoller)}.{nameof(Remove)} was called from a non-poller thread, " +
                        "so ran asynchronously. " +
                        $"The {socket.GetType().Name} being removed was disposed while the remove " +
                        $"operation waited to start on the poller thread. Use {nameof(RemoveAndDispose)} " +
                        "instead, which will enqueue the remove and dispose to happen on the poller thread.");

                socket.Socket.EventsChanged -= OnSocketEventsChanged;
                m_sockets.Remove(socket.Socket);
                m_isPollSetDirty = true;
            });
        }

        /// <summary>
        /// Remove the socket from the poller and dispose the socket
        /// </summary>
        /// <param name="socket">The socket to be removed</param>
        /// <exception cref="ArgumentNullException">If socket is null</exception>
        /// <exception cref="ArgumentException">If socket is disposed</exception>
        /// <exception cref="InvalidOperationException">If socket got disposed during the operation</exception>
        public void RemoveAndDispose<T>(T socket) where T : ISocketPollable, IDisposable
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            if (socket.IsDisposed)
                throw new ArgumentException("Must not be disposed.", nameof(socket));
            CheckDisposed();

            Run(() =>
            {
                // Ensure the socket wasn't disposed while this code was waiting to be run on the poller thread
                if (socket.IsDisposed)
                    throw new InvalidOperationException(
                        $"{nameof(NetMQPoller)}.{nameof(RemoveAndDispose)} was called from a non-poller thread, " +
                        "so ran asynchronously. " +
                        $"The {socket.GetType().Name} being removed was disposed while the remove " +
                        $"operation waited to start on the poller thread. When using {nameof(RemoveAndDispose)} " +
                        "you should not dispose the pollable object .");

                socket.Socket.EventsChanged -= OnSocketEventsChanged;
                m_sockets.Remove(socket.Socket);
                m_isPollSetDirty = true;
                socket.Dispose();
            });
        }

        /// <summary>
        /// Remove a timer from the poller
        /// </summary>
        /// <param name="timer">The timer to remove</param>
        /// <exception cref="ArgumentNullException">If poller is null</exception>
        public void Remove(NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException(nameof(timer));
            CheckDisposed();

            timer.When = -1;

            Run(() => m_timers.Remove(timer));
        }

        /// <summary>
        /// Remove the .Net socket from the poller
        /// </summary>
        /// <param name="socket">The socket to remove</param>
        /// <exception cref="ArgumentNullException">If socket is null</exception>
        public void Remove(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            CheckDisposed();

            Run(() =>
            {
                m_pollinSockets.Remove(socket);
                m_isPollSetDirty = true;
            });
        }

        #endregion

        #region Contains
#if !NET35

        /// <summary>
        /// Check if poller contains the socket asynchronously.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns>True if the poller contains the socket.</returns>
        /// <exception cref="ArgumentNullException">Thrown if socket is null</exception>
        public Task<bool> ContainsAsync(ISocketPollable socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            CheckDisposed();

            var tcs = new TaskCompletionSource<bool>();
            Run(() => tcs.SetResult(m_sockets.Contains(socket)));
            return tcs.Task;
        }

        /// <summary>
        /// Check if poller contains the timer asynchronously.
        /// </summary>
        /// <returns>True if the poller contains the timer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if timer is null</exception>
        public Task<bool> ContainsAsync(NetMQTimer timer)
        {
            if (timer == null)
                throw new ArgumentNullException(nameof(timer));
            CheckDisposed();

            var tcs = new TaskCompletionSource<bool>();
            Run(() => tcs.SetResult(m_timers.Contains(timer)));
            return tcs.Task;
        }

        /// <summary>
        /// Check if poller contains the socket asynchronously.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns>True if the poller contains the socket.</returns>
        /// <exception cref="ArgumentNullException">Thrown if socket is null</exception>
        public Task<bool> ContainsAsync(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            CheckDisposed();

            var tcs = new TaskCompletionSource<bool>();
            Run(() => tcs.SetResult(m_pollinSockets.ContainsKey(socket)));
            return tcs.Task;
        }
#endif
        #endregion

        #region Start / Stop

        /// <summary>
        /// Runs the poller in a foreground thread, returning once the poller has started.
        /// </summary>
        /// <remarks>
        /// The created thread is named <c>"NetMQPollerThread"</c>. Use <see cref="RunAsync(string)"/> to specify the thread name.
        /// </remarks>
        public void RunAsync()
        {
            RunAsync("NetMQPoller");
        }

        /// <summary>
        /// Runs the poller in a foreground thread, returning once the poller has started.
        /// </summary>
        /// <param name="threadName">The thread name to use.</param>
        public void RunAsync(string threadName)
        {
            RunAsync(threadName, false);
        }

        /// <summary>
        /// Runs the poller in a specified thread - background/foreground, returning once the poller has started.
        /// </summary>
        /// <param name="threadName">The thread name to use.</param>
        /// <param name="isBackgroundThread">Allow the poller thread to be a long running 
        /// poller (either foreground thread/background)</param>
        public void RunAsync(string threadName, bool isBackgroundThread)
        {
            CheckDisposed();
            if (IsRunning)
                throw new InvalidOperationException("NetMQPoller is already running");

            var thread = new Thread(Run)
            {
                Name = threadName,
                IsBackground = isBackgroundThread
            };
            thread.Start();

            m_switch.WaitForOn();
        }

#if NET35
        /// <summary>
        /// Runs the poller on the caller's thread. Only returns when <see cref="Stop"/> or <see cref="StopAsync"/> are called from another thread.
        /// </summary>
        public void Run()
        {
            CheckDisposed();
            if (IsRunning)
                throw new InvalidOperationException("NetMQPoller is already running");

            m_pollerThread = Thread.CurrentThread;
            m_stopSignaler.Reset();
            m_switch.SwitchOn();

            try
            {
                RunPoller();
            }
            finally
            {
                m_pollerThread = null;
                m_switch.SwitchOff();
            }
        }
#else
        /// <summary>
        /// Runs the poller on the caller's thread. Only returns when <see cref="Stop"/> or <see cref="StopAsync"/> are called from another thread.
        /// </summary>
        public void Run()
        {
            Run(new NetMQSynchronizationContext(this));
        }

        /// <summary>
        /// Runs the poller on the caller's thread. Only returns when <see cref="Stop" /> or <see cref="StopAsync" /> are called from another thread.
        /// </summary>
        /// <param name="syncContext">The synchronization context that will be used.</param>
        public void Run(SynchronizationContext syncContext)
        {
            if (syncContext == null)
                throw new ArgumentNullException("Must supply a Synchronization Context");

            CheckDisposed();
            if (IsRunning)
                throw new InvalidOperationException("NetMQPoller is already running");

            var oldSynchronisationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncContext);
            m_isSchedulerThread.Value = true;

            m_stopSignaler.Reset();
            m_switch.SwitchOn();

            try
            {
                RunPoller();
            }
            finally
            {
                m_isSchedulerThread.Value = false;
                SynchronizationContext.SetSynchronizationContext(oldSynchronisationContext);
                m_switch.SwitchOff();
            }

        }
#endif

        /// <summary>
        /// Runs the poller on the caller's thread. Only returns when <see cref="Stop"/> or <see cref="StopAsync"/> are called from another thread.
        /// </summary>
        private void RunPoller()
        {
            try
            {
                // Recalculate all timers now
                foreach (var timer in m_timers)
                {
                    if (timer.Enable)
                        timer.When = Clock.NowMs() + timer.Interval;
                }

                // Run until stop is requested
                while (!m_stopSignaler.IsStopRequested)
                {
                    if (m_isPollSetDirty)
                        RebuildPollset();

                    var pollStart = Clock.NowMs();

                    // Set tickless to "infinity"
                    long tickless = pollStart + int.MaxValue;

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

                    // Compute a timeout value - how many milliseconds from now that the earliest-timer will expire.
                    var timeout = tickless - pollStart;

                    // Use zero to indicate it has already expired.
                    if (timeout < 0)
                        timeout = 0;

                    var isItemAvailable = false;

                    Assumes.NotNull(m_pollSet);

                    if (m_pollSet.Length != 0)
                    {
                        isItemAvailable = m_netMqSelector.Select(m_pollSet, m_pollSet.Length, timeout);
                    }
                    else if (timeout > 0)
                    {
                        //TODO: Do we really want to simply sleep and return, doing nothing during this interval?
                        //TODO: Should a large value be passed it will sleep for a month literally.
                        //      Solution should be different, but sleep is more natural here than in selector (timers are not selector concern).
                        Debug.Assert(timeout <= int.MaxValue);
                        Thread.Sleep((int)timeout);
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
                        NetMQSelector.Item item = m_pollSet[i];

                        if (item.Socket != null)
                        {
                            Assumes.NotNull(m_pollact);

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
                            Assumes.NotNull(item.FileDescriptor);
                            if (m_pollinSockets.TryGetValue(item.FileDescriptor, out Action<Socket>? action))
                                action(item.FileDescriptor);
                        }
                    }
                }

                // Try to dequeue and execute all pending tasks before stopping poller
                while (m_tasksQueue.TryDequeue(out Task? task, TimeSpan.Zero))
                    TryExecuteTask(task);
            }
            finally
            {
                foreach (var socket in m_sockets.ToList())
                    Remove(socket);
            }
        }

        /// <summary>
        /// Stops the poller.
        /// </summary>
        /// <remarks>
        /// If called from a thread other than the poller thread, this method will block until the poller has stopped.
        /// If called from the poller thread it is not possible to block.
        /// </remarks>
        public void Stop()
        {
            CheckDisposed();

            // Signal the poller to stop
            m_stopSignaler.RequestStop();

            // If 'stop' was requested from the scheduler thread, we cannot block
            if (!IsPollerThread)
            {
                m_switch.WaitForOff();
                Debug.Assert(!IsRunning);
            }
        }

        /// <summary>
        /// Stops the poller, returning immediately and most likely before the poller has actually stopped.
        /// </summary>
        public void StopAsync()
        {
            CheckDisposed();
            if (!IsRunning)
                throw new InvalidOperationException("NetMQPoller is not running");
            m_stopSignaler.RequestStop();
        }

        #endregion

        private void OnSocketEventsChanged(object? sender, NetMQSocketEventArgs e)
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
            m_pollSet = new NetMQSelector.Item[m_sockets.Count + m_pollinSockets.Count];
            m_pollact = new NetMQSocket[m_sockets.Count];

            // For each socket in m_sockets,
            // put a corresponding SelectItem into the m_pollSet array and a reference to the socket itself into the m_pollact array.
            uint index = 0;

            foreach (var socket in m_sockets)
            {
                m_pollSet[index] = new NetMQSelector.Item(socket, socket.GetPollEvents());
                m_pollact[index] = socket;
                index++;
            }

            foreach (var socket in m_pollinSockets.Keys)
            {
                m_pollSet[index] = new NetMQSelector.Item(socket, PollEvents.PollError | PollEvents.PollIn);
                index++;
            }

            // Mark this as NOT having any fresh events to attend to, as yet.
            m_isPollSetDirty = false;
        }

        #region IEnumerable

        /// <summary>This class only implements <see cref="IEnumerable"/> in order to support collection initialiser syntax.</summary>
        /// <returns>An empty enumerator.</returns>
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
            if (IsDisposed)
                throw new ObjectDisposedException("NetMQPoller");
        }

        /// <summary>
        /// Stops and disposes the poller. The poller may not be used once disposed.
        /// </summary>
        /// <remarks>
        /// Note that you cannot dispose the poller on the poller's thread. Doing so immediately throws an exception.
        /// </remarks>
        /// <exception cref="NetMQException">A socket in the poller has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Dispose called from the poller thread.</exception>
        public void Dispose()
        {
            // Attempting to dispose from the poller thread would cause a deadlock.
            // Throw an exception to improve the debugging experience.
            if (IsPollerThread)
                throw new InvalidOperationException("Cannot dispose poller from the poller thread.");

            if (Interlocked.CompareExchange(ref m_disposeState, (int)DisposeState.Disposing, (int)DisposeState.Undisposed) != (int)DisposeState.Undisposed)
                return;

            // If this poller is already started, signal the polling thread to stop
            // and wait for it.
            if (IsRunning)
            {
                m_stopSignaler.RequestStop();
                m_switch.WaitForOff();
                Debug.Assert(!IsRunning);
            }

            m_sockets.Remove(((ISocketPollable)m_stopSignaler).Socket);
            m_stopSignaler.Dispose();
#if !NET35
            m_sockets.Remove(((ISocketPollable)m_tasksQueue).Socket);
            m_tasksQueue.Dispose();
#endif

            foreach (var socket in m_sockets)
            {
                if (socket.IsDisposed)
                    throw new NetMQException($"Invalid state detected: {nameof(NetMQPoller)} contains a disposed {nameof(NetMQSocket)}. Sockets must be either removed before being disposed, or disposed after the poller is disposed.");
                socket.EventsChanged -= OnSocketEventsChanged;
            }

            m_disposeState = (int)DisposeState.Disposed;
        }

        #endregion

        #region ISynchronizeInvoke

#if NET40
        IAsyncResult ISynchronizeInvoke.BeginInvoke(Delegate method, object[] args)
        {
            var task = new Task<object>(() => method.DynamicInvoke(args));
            task.Start(this);
            return task;
        }

        object ISynchronizeInvoke.EndInvoke(IAsyncResult result)
        {
            var task = (Task<object>)result;
            return task.Result;
        }

        object ISynchronizeInvoke.Invoke(Delegate method, object[] args)
        {
            if (CanExecuteTaskInline)
                return method.DynamicInvoke(args);

            var task = new Task<object>(() => method.DynamicInvoke(args));
            task.Start(this);
            return task.Result;
        }

        bool ISynchronizeInvoke.InvokeRequired => !CanExecuteTaskInline;
#endif

        #endregion
    }
}
