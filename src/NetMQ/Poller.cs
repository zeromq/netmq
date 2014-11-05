using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using NetMQ.Core;
using NetMQ.Core.Patterns;
using NetMQ.Utils;

namespace NetMQ
{
    public class Poller : IDisposable
    {
        class PollinSocket
        {
            public PollinSocket(Socket socket, Action<Socket> action)
            {
                Socket = socket;
                Action = action;
            }

            public Socket Socket { get; private set; }
            public Action<Socket> Action { get; private set; }
        }

        private readonly IList<NetMQSocket> m_sockets = new List<NetMQSocket>();
        private readonly IDictionary<Socket, Action<Socket>> m_pollinSockets = new Dictionary<Socket, Action<Socket>>();

        readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
        readonly List<NetMQTimer> m_zombies = new List<NetMQTimer>();

        private int m_cancel;
        readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);
        private bool m_isStarted;

        private bool m_isDirty = true;
        private bool m_disposed = false;

        List<Socket> m_writeList = new List<Socket>();
        List<Socket> m_readList = new List<Socket>();
        List<Socket> m_errorList = new List<Socket>();

        private List<NetMQSocket> m_finalSockets = new List<NetMQSocket>();
        private List<PollinSocket> m_finalPollinSockets = new List<PollinSocket>();

        public Poller()
        {
            PollTimeout = 1000;

            m_cancel = 0;
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

            m_sockets.Remove(socket.Socket);
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
            PollWhile(() => m_cancel == 0);
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

            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "NetMQPollerThread";

            m_isStoppedEvent.Reset();
            m_isStarted = true;

            bool eventsReady = true;

            try
            {
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
                        m_finalPollinSockets.Clear();
                        m_finalSockets.Clear();

                        foreach (var socket in m_sockets)
                        {
                            m_finalSockets.Add(socket);
                        }

                        foreach (var pollinSocket in m_pollinSockets)
                        {
                            m_finalPollinSockets.Add(new PollinSocket(pollinSocket.Key, pollinSocket.Value));
                        }

                        m_isDirty = false;
                    }

                    m_readList.Clear();
                    m_writeList.Clear();
                    m_errorList.Clear();

                    foreach (var socket in m_sockets)
                    {
                        if (socket.GetPollEvents() != PollEvents.None &&
                            (socket.SocketHandle.FD.ProtocolType == ProtocolType.Tcp || socket.SocketHandle.FD.Connected))
                        {
                            m_readList.Add(socket.SocketHandle.FD);
                        }
                    }

                    foreach (var pollinSocket in m_pollinSockets.Keys)
                    {
                        m_readList.Add(pollinSocket);
                    }

                    var pollStart = Clock.NowMs();
                    var timeout = TicklessTimer();

                    if (m_readList.Count == 0 && m_errorList.Count == 0 && m_writeList.Count == 0)
                    {
                        Thread.Sleep(timeout);
                    }
                    else
                    {
                        int selectTimeout;

                        // if events are ready we don't wait
                        if (eventsReady)
                        {
                            selectTimeout = 0;
                        }
                        else if (timeout == -1)
                        {
                            selectTimeout = -1;
                        }
                        else
                        {
                            selectTimeout = timeout * 1000;
                        }

                        try
                        {
                            Socket.Select(m_readList, m_writeList, m_errorList, selectTimeout);
                        }
                        catch (SocketException ex)
                        {
                            throw NetMQException.Create(ErrorCode.ESOCKET, ex);
                        }
                    }


                    bool empty = m_readList.Count == 0 && m_errorList.Count == 0 && m_writeList.Count == 0;

                    // Get the expected end time in case we time out. This looks redundant but, unfortunately,
                    // it happens that Poll takes slightly less than the requested time and 'Clock.NowMs() >= timer.When'
                    // may not true, even if it is supposed to be. In other words, even when Poll times out, it happens
                    // that 'Clock.NowMs() < pollStart + timeout'
                    var expectedPollEnd = empty ? pollStart + timeout : -1;

                    // that way we make sure we can continue the loop if new timers are added.
                    // timers cannot be removed                    
                    foreach (var timer in m_timers)
                    {
                        if ((Clock.NowMs() >= timer.When || expectedPollEnd >= timer.When) && timer.When != -1)
                        {
                            timer.InvokeElapsed(this);

                            if (timer.Enable)
                            {
                                timer.When = timer.Interval + Clock.NowMs();
                            }
                        }
                    }

                    eventsReady = false;

                    foreach (var socket in m_finalSockets)
                    {
                        eventsReady |= socket.InvokeEvents(this);
                    }

                    foreach (var pollinSocket in m_finalPollinSockets)
                    {
                        if (m_readList.Contains(pollinSocket.Socket))
                        {
                            pollinSocket.Action(pollinSocket.Socket);
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

        public void Stop()
        {
            Stop(true);
        }
    }
}
