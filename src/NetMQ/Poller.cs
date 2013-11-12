// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Collections.Generic;
using System.Linq;
#if PRE_4
using System.Net.Sockets;
using System.Text;
#endif
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

        private readonly IList<NetMQSocket> m_sockets = new List<NetMQSocket>();

        private PollItem[] m_pollset;
        private NetMQSocket[] m_pollact;
        private int m_pollSize;

        readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
        readonly List<NetMQTimer> m_zombies = new List<NetMQTimer>();

#if !PRE_4
        readonly CancellationTokenSource m_cancellationTokenSource;
#else
        /// <summary>
        /// This is a flag that indicates a request has been made to stop (cancel) the socket monitoring.
        /// Zero represents false, 1 represents true - cancellation is requested.
        /// </summary>
        private long m_isCancellationRequested;
#endif

        readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);

        /// <summary>
        /// This flag indicates whether this Poller has started.
        /// </summary>
        private bool m_isStarted;

        private bool m_isDirty = true;

        /// <summary>
        /// Create a new Poller object, with a default PollTimeout of 1 second.
        /// </summary>
        public Poller()
        {
            PollTimeout = 1000;
#if !PRE_4
            m_cancellationTokenSource = new CancellationTokenSource();
#endif
        }

        /// <summary>
        /// Get or set the poll timeout in milliseconds.
        /// </summary>
        public int PollTimeout { get; set; }

        /// <summary>
        /// Get whether the polling has started.
        /// This is set true at the beginning of the Start method, and cleared in the Stop method.
        /// </summary>
        public bool IsStarted { get { return m_isStarted; } }

        public void AddSocket(NetMQSocket socket)
        {
            if (m_sockets.Contains(socket))
            {
                throw new ArgumentException("Socket already added to poller");
            }

            m_sockets.Add(socket);

            socket.EventsChanged += OnSocketEventsChanged;

            m_isDirty = true;
        }

        public void RemoveSocket(NetMQSocket socket)
        {
            socket.EventsChanged -= OnSocketEventsChanged;

            m_sockets.Remove(socket);
            m_isDirty = true;
        }

        private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
        {
            // when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
            m_isDirty = true;
        }

        public void AddTimer(NetMQTimer timer)
        {
            m_timers.Add(timer);
        }

        public void RemoveTimer(NetMQTimer timer)
        {
            timer.When = -1;
            m_zombies.Add(timer);
        }

        private void RebuildPollset()
        {
            m_pollset = null;
            m_pollact = null;

            m_pollSize = m_sockets.Count;
            m_pollset = new PollItem[m_pollSize];
            m_pollact = new NetMQSocket[m_pollSize];

            uint itemNbr = 0;
            foreach (var socket in m_sockets)
            {
                m_pollset[itemNbr] = new PollItem(socket.SocketHandle, socket.GetPollEvents());
                m_pollact[itemNbr] = socket;
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
            // Set the name of this thread, unless it has already been set.
            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "NetMQPollerThread";
            }

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

                while (!IsCancellationRequested)
                {
                    if (m_isDirty)
                    {
                        RebuildPollset();
                    }

                    ZMQ.Poll(m_pollset, m_pollSize, TicklessTimer());

                    // that way we make sure we can continue the loop if new timers are added.
                    // timers cannot be removed
                    int timersCount = m_timers.Count;
                    for (int i = 0; i < timersCount; i++)
                    {
                        var timer = m_timers[i];

                        if (Clock.NowMs() >= timer.When && timer.When != -1)
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
                        NetMQSocket socket = m_pollact[itemNbr];
                        PollItem item = m_pollset[itemNbr];

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
            }
        }

        #region IsCancellationRequested
        /// <summary>
        /// Get whether a request to stop polling has been made.
        /// </summary>
        private bool IsCancellationRequested
        {
            get
            {
#if !PRE_4
                return m_cancellationTokenSource.IsCancellationRequested;
#else
                return Interlocked.Read(ref m_isCancellationRequested) != 0;
#endif
            }
        }
        #endregion

        #region RequestCancellation
        /// <summary>
        /// Set a flag that indicates that we want to stop ("cancel") polling.
        /// </summary>
        private void RequestCancellation()
        {
#if !PRE_4
            m_cancellationTokenSource.Cancel();
#else
            // Set the cancellation-flag to the value that we are using to represent true.
            Interlocked.Exchange(ref m_isCancellationRequested, 1);
#endif
        }
        #endregion

        /// <summary>
        /// Stop the poller job, it may take awhile until the poller is fully stopped.
        /// If it doesn't stop within 20 seconds, it times-out anyway and returns.
        /// </summary>
        /// <param name="waitForCloseToComplete">if true the method will block until the poller is fully stopped</param>
        public void Stop(bool waitForCloseToComplete)
        {
            RequestCancellation();

            if (waitForCloseToComplete)
            {
                m_isStoppedEvent.WaitOne();
            }

            m_isStarted = false;
        }

        /// <summary>
        /// Stop the poller job. This returns after waiting for that thread to stop.
        /// This is equivalent to calling Stop(waitForCloseToComplete: true).
        /// </summary>
        public void Stop()
        {
            Stop(true);
        }
    }
}
