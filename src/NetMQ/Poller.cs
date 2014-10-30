using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using NetMQ.zmq;

namespace NetMQ
{
    public class Poller : IDisposable
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

        private readonly IList<NetMQSocket> m_sockets = new List<NetMQSocket>();
        private readonly IDictionary<Socket, Action<Socket>> m_pollinSockets = new Dictionary<Socket, Action<Socket>>();        

        private PollItem[] m_pollset;
        private NetMQSocket[] m_pollact;
        private int m_pollSize;

        readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
        readonly List<NetMQTimer> m_zombies = new List<NetMQTimer>();

        readonly CancellationTokenSource m_cancellationTokenSource;
        readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);
        private bool m_isStarted;

        private bool m_isDirty = true;
        private bool m_disposed = false;

        public Poller()
        {
            PollTimeout = 1000;

            m_cancellationTokenSource = new CancellationTokenSource();
        }

        public Poller(params ISocketPollable[] sockets)
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

        public Poller(params NetMQTimer[] timers)
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

        #region Dtors

        ~Poller()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    if (m_isStarted)
                    {
                        Stop(false);
                    }
                }

                m_disposed = true;
            }
        }

        #endregion

        /// <summary>
        /// Poll timeout in milliseconds
        /// </summary>
        public int PollTimeout { get; set; }

        public bool IsStarted { get { return m_isStarted; } }

        public void AddPollInSocket(Socket socket, Action<Socket> callback)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
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

        public void RemovePollInSocket(Socket socket)
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

        public void AddSocket(ISocketPollable socket)
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

        public void RemoveSocket(ISocketPollable socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("stock");
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            socket.Socket.EventsChanged -= OnSocketEventsChanged;

            m_sockets.Remove(socket.Socket);
            m_isDirty = true;
        }

        private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
        {
            // when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
            m_isDirty = true;
        }

        public void AddTimer(NetMQTimer timer)
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

        public void RemoveTimer(NetMQTimer timer)
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

        private void RebuildPollset()
        {
            m_pollset = null;
            m_pollact = null;

            m_pollSize = m_sockets.Count + m_pollinSockets.Count;
            m_pollset = new PollItem[m_pollSize];
            
            m_pollact = new NetMQSocket[m_sockets.Count];

            uint itemNbr = 0;
            foreach (var socket in m_sockets)
            {
                m_pollset[itemNbr] = new PollItem(socket.SocketHandle, socket.GetPollEvents());
                m_pollact[itemNbr] = socket;
                itemNbr++;
            }

            foreach (var socket in m_pollinSockets)
            {
                m_pollset[itemNbr] = new PollItem(socket.Key, PollEvents.PollError | PollEvents.PollIn);                
                itemNbr++;
            }

            m_isDirty = false;
        }


        int TicklessTimer()
        {
            //  Calculate tickless timer
            Int64 tickless = Clock.NowMs() + PollTimeout;

            foreach (NetMQTimer timer in m_timers)
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

            int timeout = (int)(tickless - Clock.NowMs());
            if (timeout < 0)
            {
                timeout = 0;
            }

            return timeout;
        }

        public void Start()
        {
            PollWhile(() => !m_cancellationTokenSource.IsCancellationRequested);
        }

        public void PollOnce()
        {
            int timesToPoll = 1;
            PollWhile(() => timesToPoll-- > 0);
        }

        private void PollWhile(Func<bool> condition)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            if (m_isStarted)
            {
                throw new InvalidOperationException("Poller is started");
            }

            if(Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "NetMQPollerThread";

            m_isStoppedEvent.Reset();
            m_isStarted = true;
            try
            {
                // the sockets may have been created in another thread, to make sure we can fully use them we do full memory barried
                // at the begining of the loop
                Thread.MemoryBarrier();

                //  Recalculate all timers now
                foreach (NetMQTimer netMQTimer in m_timers)
                {
                    if (netMQTimer.Enable)
                    {
                        netMQTimer.When = Clock.NowMs() + netMQTimer.Interval;
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

                    var nbEvents = ZMQ.Poll(m_pollset, m_pollSize, timeout);

                    // Get the expected end time in case we time out. This looks redundant but, unfortunately,
                    // it happens that Poll takes slightly less than the requested time and 'Clock.NowMs() >= timer.When'
                    // may not true, even if it is supposed to be. In other words, even when Poll times out, it happens
                    // that 'Clock.NowMs() < pollStart + timeout'
                    var expectedPollEnd = nbEvents == 0 ? pollStart + timeout : -1;

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
                        PollItem item = m_pollset[itemNbr];

                        if (item.Socket != null)
                        {
                            NetMQSocket socket = m_pollact[itemNbr] as NetMQSocket;
                            
                            if (item.ResultEvent.HasFlag(PollEvents.PollError) && !socket.IgnoreErrors)
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
                        foreach (NetMQTimer netMQTimer in m_zombies)
                        {
                            m_timers.Remove(netMQTimer);
                        }

                        m_zombies.Clear();
                    }
                }
            }
            finally
            {
                m_isStoppedEvent.Set();

                foreach (var socket in m_sockets.ToList())
                {
                    RemoveSocket(socket);
                }
            }
        }

        /// <summary>
        /// Stop the poller job, it may take a while until the poller is fully stopped
        /// </summary>
        /// <param name="waitForCloseToComplete">if true the method will block until the poller is fully stopped</param>
        public void Stop(bool waitForCloseToComplete)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException("Poller is disposed");
            }

            if (m_isStarted)
            {
                m_cancellationTokenSource.Cancel();

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

        public void Stop()
        {
            Stop(true);
        }


    }
}
