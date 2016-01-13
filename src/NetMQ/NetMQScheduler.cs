using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetMQ.Core;
using NetMQ.Sockets;

namespace NetMQ
{
    [Obsolete("Use NetMQPoller instead")]
    public class NetMQScheduler : TaskScheduler, IDisposable
    {
        private static int s_schedulerCounter;

        /// <summary>
        /// True if we own m_poller (that is, it was created within the NetMQScheduler constructor
        /// as opposed to being passed-in from the caller).
        /// </summary>
        private readonly bool m_ownPoller;

        private readonly Poller m_poller;

        private readonly NetMQSocket m_serverSocket;
        private readonly NetMQSocket m_clientSocket;

        private readonly ThreadLocal<bool> m_isSchedulerThread;

        private readonly ConcurrentQueue<Task> m_tasksQueue = new ConcurrentQueue<Task>();

        private readonly object m_syncObject = new object();

        private EventHandler<NetMQSocketEventArgs> m_currentMessageHandler;

        /// <summary>
        /// Create a new NetMQScheduler object within the given context, and optionally using the given poller.
        /// </summary>
        /// <param name="context">the NetMQContext to create this NetMQScheduler within</param>
        /// <param name="poller">(optional)the Poller for this Net to use</param>
        [Obsolete("Use non context version")]
        public NetMQScheduler([NotNull] NetMQContext context, [CanBeNull] Poller poller = null) : 
            this(poller, context.CreatePushSocket(), context.CreatePullSocket())
        {
            
        }

        /// <summary>
        /// Create a new NetMQScheduler object within the given context, and optionally using the given poller.
        /// </summary>        
        /// <param name="poller">(optional)the Poller for this Net to use</param>        
        public NetMQScheduler([CanBeNull] Poller poller = null) :
            this(poller, new PushSocket(), new PullSocket())
        {

        }

        private NetMQScheduler(Poller poller, PushSocket pushSocket, PullSocket pullSocket)
        {
            if (poller == null)
            {
                m_ownPoller = true;
                m_poller = new Poller();
            }
            else
            {
                m_ownPoller = false;
                m_poller = poller;
            }

            var address = string.Format("{0}://scheduler-{1}",
                Address.InProcProtocol,
                Interlocked.Increment(ref s_schedulerCounter));

            m_serverSocket = pullSocket;
            m_serverSocket.Options.Linger = TimeSpan.Zero;
            m_serverSocket.Bind(address);

            m_currentMessageHandler = OnMessageFirstTime;

            m_serverSocket.ReceiveReady += m_currentMessageHandler;

            m_poller.AddSocket(m_serverSocket);

            m_clientSocket = pushSocket;
            m_clientSocket.Connect(address);

            m_isSchedulerThread = new ThreadLocal<bool>(() => false);

            if (m_ownPoller)
            {
                m_poller.PollTillCancelledNonBlocking();
            }
        }

        private void OnMessageFirstTime(object sender, NetMQSocketEventArgs e)
        {
            // set the current thread as the scheduler thread, this only happens the first time a message arrived and is important for the TryExecuteTaskInline
            m_isSchedulerThread.Value = true;

            // stop calling the OnMessageFirstTime and start calling OnMessage
            m_serverSocket.ReceiveReady -= m_currentMessageHandler;
            m_currentMessageHandler = OnMessage;
            m_serverSocket.ReceiveReady += m_currentMessageHandler;

            OnMessage(sender, e);
        }

        private void OnMessage(object sender, NetMQSocketEventArgs e)
        {
            // remove the awake command from the queue
            m_serverSocket.SkipFrame();

            Task task;

            while (m_tasksQueue.TryDequeue(out task))
            {
                TryExecuteTask(task);
            }
        }

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

        #region Task scheduler

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return CanExecuteTaskInline && TryExecuteTask(task);
        }

        /// <summary>
        /// Get the maximum level of concurrency used in the scheduling.
        /// This is simply the value 1, and is not presently used anywhere within NetMQ.
        /// </summary>
        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        /// <summary>
        /// Return a collection of the scheduled Tasks.  (Not supported - for debug purposes only)
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // this is not supported, also it's only important for debug purposes and doesn't get called in real time.
            throw new NotSupportedException();
        }

        protected override void QueueTask(Task task)
        {
            m_tasksQueue.Enqueue(task);

            lock (m_syncObject)
            {
                // awake the scheduler
                m_clientSocket.SendFrameEmpty();
            }
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        /// <param name="disposing">set this to true if releasing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (!m_ownPoller && !m_poller.IsStarted)
            {
                DisposeSynced();
                return;
            }

            // disposing on the scheduler thread
            var task = new Task(DisposeSynced);
            task.Start(this);
            task.Wait();

            // poller cannot be stopped from poller thread
            if (m_ownPoller)
            {
                m_poller.CancelAndJoin();
                m_poller.Dispose();
            }
            m_isSchedulerThread.Dispose();
        }

        private void DisposeSynced()
        {
            Thread.MemoryBarrier();

            m_poller.RemoveSocket(m_serverSocket);

            m_serverSocket.ReceiveReady -= m_currentMessageHandler;

            m_serverSocket.Dispose();
            m_clientSocket.Dispose();
        }

        #endregion
    }
}
