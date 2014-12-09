using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMQ
{
    public class NetMQScheduler : TaskScheduler, IDisposable
    {
        private readonly bool m_ownPoller;
        private readonly Poller m_poller;

        private static int s_schedulerCounter = 0;

        private readonly int m_schedulerId;
        private readonly string m_address;

        private readonly NetMQContext m_context;
        private readonly NetMQSocket m_serverSocket;
        private readonly NetMQSocket m_clientSocket;

        private ThreadLocal<bool> m_schedulerThread;

        private ConcurrentQueue<Task> m_tasksQueue;

        private object m_syncObject;

        private EventHandler<NetMQSocketEventArgs> m_currentMessageHandler;

        public NetMQScheduler(NetMQContext context, Poller poller = null)
        {
            m_context = context;
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

            m_tasksQueue = new ConcurrentQueue<Task>();
            m_syncObject = new object();

            m_schedulerId = Interlocked.Increment(ref s_schedulerCounter);

            m_address = string.Format("{0}://scheduler-{1}", NetMQ.zmq.Address.InProcProtocol, m_schedulerId);

            m_serverSocket = context.CreatePullSocket();
            m_serverSocket.Options.Linger = TimeSpan.Zero;
            m_serverSocket.Bind(m_address);

            m_currentMessageHandler = OnMessageFirstTime;

            m_serverSocket.ReceiveReady += m_currentMessageHandler;

            m_poller.AddSocket(m_serverSocket);

            m_clientSocket = m_context.CreatePushSocket();
            m_clientSocket.Connect(m_address);

            m_schedulerThread = new ThreadLocal<bool>(() => false);

            if (m_ownPoller)
            {
                m_poller.PollTillCancelledNonBlocking();                
            }
        }

        private void OnMessageFirstTime(object sender, NetMQSocketEventArgs e)
        {
            // set the current thread as the scheduler thread, this only happen the first time message arrived and important for the TryExecuteTaskInline
            m_schedulerThread.Value = true;

            // stop calling the OnMessageFirstTime and start calling OnMessage
            m_serverSocket.ReceiveReady -= m_currentMessageHandler;
            m_currentMessageHandler = OnMessage;
            m_serverSocket.ReceiveReady += m_currentMessageHandler;

            OnMessage(sender, e);
        }

        private void OnMessage(object sender, NetMQSocketEventArgs e)
        {
            // remove the awake command from the queue
            m_serverSocket.Receive();

            Task task;

            while (m_tasksQueue.TryDequeue(out task))
            {
                TryExecuteTask(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return m_schedulerThread.Value && TryExecuteTask(task);
        }

        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        public void Dispose()
        {
            // disposing on the scheduler thread
            Task task = new Task(DisposeSynced);
            task.Start(this);
            task.Wait();

            // poller cannot be stopped from poller thread
            if (m_ownPoller)
            {
                m_poller.CancelAndJoin();
            }
        }

        private void DisposeSynced()
        {
            Thread.MemoryBarrier();

            m_poller.RemoveSocket(m_serverSocket);

            m_serverSocket.ReceiveReady -= m_currentMessageHandler;

            m_serverSocket.Dispose();
            m_clientSocket.Dispose();
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // this is not supported, also it's only important for debug propose and doesn't get called in real time
            throw new NotSupportedException();
        }

        protected override void QueueTask(Task task)
        {
            m_tasksQueue.Enqueue(task);

            lock (m_syncObject)
            {
                // awake the scheduler
                m_clientSocket.Send("");
            }
        }
    }
}
