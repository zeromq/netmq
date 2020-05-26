#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET47

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NetMQ
{
    /// <summary>
    /// NetMQRuntime enable using NetMQSocket receive async methods.
    /// You need to create an instance before calling any async methods.
    /// To continue and process the Tasks call <see cref="Run(Task[])" /> and <see cref="Run(CancellationToken, Task[])"/>
    /// </summary>
    public class NetMQRuntime : IDisposable
    {
        private NetMQPoller m_poller;
        private readonly NetMQSynchronizationContext m_synchronizationContext;
        private readonly SynchronizationContext m_oldSynchronizationContext;
        private static readonly ThreadLocal<NetMQRuntime> s_current = new ThreadLocal<NetMQRuntime>();
        private readonly List<NetMQSocket> m_sockets;

        /// <summary>
        /// Create a new NetMQRuntime, you can start calling async method after creating a runtime.
        /// </summary>
        public NetMQRuntime()
        {
            m_poller = new NetMQPoller();
            m_sockets = new List<NetMQSocket>();
            m_synchronizationContext = new NetMQSynchronizationContext(m_poller);
            m_oldSynchronizationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(m_synchronizationContext);
            s_current.Value = this;
        }

        /// <summary>
        /// The current thread NetMQRuntime
        /// </summary>
        public static NetMQRuntime Current
        {
            get { return s_current.Value; }
        }

        internal static NetMQPoller Poller
        {
            get { return Current.m_poller; }
        }

        /// <summary>
        /// Run the tasks to completion
        /// </summary>
        /// <param name="tasks">The list of tasks to run</param>
        public void Run(params Task[] tasks)
        {
            Run(CancellationToken.None, tasks);
        }
        
        internal void Add(NetMQSocket socket)
        {
            m_poller.Add(socket);
            m_sockets.Add(socket);
        }

        internal void Remove(NetMQSocket socket)
        {
            m_poller.Remove(socket);
            m_sockets.Remove(socket);
        }

        /// <summary>
        /// Run the tasks to completion
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the run operation before all tasks run to completion</param>
        /// <param name="tasks">The list of tasks to run</param>
        public void Run(CancellationToken cancellationToken, params Task[] tasks)
        {
            var registration = cancellationToken.Register(() => m_poller.StopAsync(), false);

            Task.WhenAll(tasks).ContinueWith(t => m_poller.Stop(), cancellationToken);

            m_poller.Run(m_synchronizationContext);

            registration.Dispose();
        }

        /// <summary>
        /// Dispose the runtime, don't call Async method after disposing
        /// </summary>
        public void Dispose()
        {
            foreach (var socket in m_sockets)
            {
                socket.DetachFromRuntime();
            }

            m_poller.Dispose();
            SynchronizationContext.SetSynchronizationContext(m_oldSynchronizationContext);
        }
    }
}

#endif