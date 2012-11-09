using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;
using System.Threading;

namespace NetMQ
{
    public class Poller
    {
        class ProxyPoll
        {
            public BaseSocket FromSocket { get; set; }

            public BaseSocket ToSocket { get; set; }
        }

        class SocketPoll
        {
            public BaseSocket Socket { get; set; }
            public Action<BaseSocket> Action;
        }

        

        CancellationTokenSource m_cancellationTokenSource;

        IList<SocketPoll> m_sockets = new List<SocketPoll>();

        IList<ProxyPoll> m_proxies = new List<ProxyPoll>();

        IList<MonitorPoll> m_monitors = new List<MonitorPoll>();

        public Poller()
        {
            CloseSocketsOnStop = true;
            PollTimeout = TimeSpan.FromSeconds(1);
        }

        public bool CloseSocketsOnStop { get; set; }

        public TimeSpan PollTimeout { get; set; }

        public void AddSocket<T>(T socket, Action<T> action) where T: BaseSocket
        {
            m_sockets.Add(new SocketPoll 
                {
                    Socket = socket,
                    Action = s => action(s as T)
                });
        }

        public void AddProxy(BaseSocket from, BaseSocket to, bool duplex)
        {
            m_proxies.Add(new ProxyPoll { FromSocket = from, ToSocket = to });

            if (duplex)
            {
                m_proxies.Add(new ProxyPoll { FromSocket = to, ToSocket = from });
            }
        }

        public void AddMonitor(MonitorPoll poll)
        {            
            m_monitors.Add(poll);
        }

        public void Start()
        {
            // the sockets may have been created in another thread, to make sure we can fully use them we do full memory barried
            // at the begining of the loop
            Thread.MemoryBarrier();

            PollItem[] items = new PollItem[m_proxies.Count + m_sockets.Count + m_monitors.Count];
            int i = 0;

            foreach (var item in m_sockets)
            {
                items[i] = new PollItem(item.Socket.SocketHandle, PollEvents.PollIn);
                i++;
            }

            foreach (var item in m_proxies)
            {
                items[i] = new PollItem(item.FromSocket.SocketHandle, PollEvents.PollIn);
                i++;
            }
           
            Dictionary<SocketBase, SocketPoll> handleToSocket = m_sockets.ToDictionary(k => k.Socket.SocketHandle, v => v);
            Dictionary<SocketBase, ProxyPoll> handleToProxy = m_proxies.ToDictionary(k => k.FromSocket.SocketHandle, v => v);

            foreach (MonitorPoll poll in m_monitors)
            {
                poll.CreateMonitoringSocket();
                items[i] = new PollItem(poll.MonitoringSocket.SocketHandle, PollEvents.PollIn);
                i++;
            }

            Dictionary<SocketBase, MonitorPoll> handleToMonitor = m_monitors.ToDictionary(k => k.MonitoringSocket.SocketHandle, m => m);

            while (!m_cancellationTokenSource.IsCancellationRequested)
            {
                int result = ZMQ.Poll(items,(int) PollTimeout.TotalMilliseconds);

                foreach (var item in items)
                {
                    if ((item.ResultEvent & PollEvents.PollIn) == PollEvents.PollIn)
                    {
                        SocketPoll socketPoll;
                        ProxyPoll proxyPoll;
                        MonitorPoll monitorPoll;

                        if (handleToSocket.TryGetValue(item.Socket, out socketPoll))
                        {
                            // calling the registed delegate to do the read from the socket
                            socketPoll.Action(socketPoll.Socket);
                        }
                        else if (handleToProxy.TryGetValue(item.Socket, out proxyPoll))
                        {                            
                            Msg msg = ZMQ.Recv(proxyPoll.FromSocket.SocketHandle, SendRecieveOptions.None);
                            
                            bool more = ZMQ.ZmqMsgGet(msg, MsgFlags.More) == 1;

                            ZMQ.Send(proxyPoll.ToSocket.SocketHandle, msg, more ? SendRecieveOptions.SendMore : SendRecieveOptions.None);

                            while (more)
                            {
                                msg = ZMQ.Recv(proxyPoll.FromSocket.SocketHandle, SendRecieveOptions.None);

                                more = ZMQ.ZmqMsgGet(msg, MsgFlags.More) == 1;

                                ZMQ.Send(proxyPoll.ToSocket.SocketHandle, msg, more ? SendRecieveOptions.SendMore : SendRecieveOptions.None);
                            }
                        }
                        else if (handleToMonitor.TryGetValue(item.Socket, out monitorPoll))
                        {
                            monitorPoll.Handle();
                        }
                    }
                }
            }

            if (CloseSocketsOnStop)
            {
                // make a list of all the sockets the poller need to close
                var socketToClose =
                m_monitors.Select(m => m.Socket).Union(m_proxies.Select(p => p.FromSocket)).
                    Union(m_proxies.Select(p => p.ToSocket)).
                    Union(m_monitors.Select(m => m.Socket)).Distinct();

                foreach (var socket in socketToClose)
                {
                    socket.Close();
                }
            }
        }

        public void Stop()
        {
            m_cancellationTokenSource.Cancel();
        }
    }
}
