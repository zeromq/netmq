#if NETSTANDARD2_0 || NET47

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace NetMQ
{
    public class NetMQRuntime : IDisposable
    {
        private NetMQPoller m_poller;
        private readonly NetMQSynchronizationContext m_synchronizationContext;
        private readonly SynchronizationContext m_oldSynchronizationContext;
        private static readonly ThreadLocal<NetMQRuntime> s_current = new ThreadLocal<NetMQRuntime>();
        private List<NetMQSocket> m_sockets;

        public NetMQRuntime()
        {
            m_poller = new NetMQPoller();
            m_sockets = new List<NetMQSocket>();
            m_synchronizationContext = new NetMQSynchronizationContext(m_poller);
            m_oldSynchronizationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(m_synchronizationContext);
            s_current.Value = this;
        }

        public static NetMQRuntime Current
        {
            get { return s_current.Value; }
        }

        internal static NetMQPoller Poller
        {
            get { return Current.m_poller; }
        }

        public void Run(params Task[] tasks)
        {
            Run(CancellationToken.None, tasks);
        }

        public void Add(NetMQSocket socket)
        {
            m_poller.Add(socket);
            m_sockets.Add(socket);
        }

        public void Remove(NetMQSocket socket)
        {
            m_poller.Remove(socket);
            m_sockets.Remove(socket);
        }

        public void Run(CancellationToken cancellationToken, params Task[] tasks)
        {
            var registration = cancellationToken.Register(() => m_poller.Stop(), true);

            Task.WhenAll(tasks).ContinueWith(t => m_poller.Stop(), cancellationToken);

            m_poller.Run(m_synchronizationContext);

            registration.Dispose();
        }

        public void Dispose()
        {
            foreach (var socket in m_sockets)
            {
                m_poller.Remove(socket);
                socket.DetachFromRuntime();
            }

            m_poller.Dispose();
            SynchronizationContext.SetSynchronizationContext(m_oldSynchronizationContext);
        }
    }
}

#endif