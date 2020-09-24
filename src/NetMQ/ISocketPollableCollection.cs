using System;
using NetMQ.Monitoring;

namespace NetMQ
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// This interface provides an abstraction over the legacy Poller and newer <see cref="NetMQPoller"/> classes for use in <see cref="NetMQMonitor"/>.
    /// </remarks>
    [Obsolete("Use INetMQPoller instead")]
    public interface ISocketPollableCollection
    {
        /// <summary>
        /// Add a socket to a poller
        /// </summary>
        /// <param name="socket"></param>
        void Add(ISocketPollable socket);
        
        /// <summary>
        /// Remove a socket from poller
        /// </summary>
        /// <param name="socket"></param>
        void Remove(ISocketPollable socket);
        
        /// <summary>
        /// Remove a socket and dispose it
        /// </summary>
        /// <param name="socket"></param>
        /// <typeparam name="T"></typeparam>
        void RemoveAndDispose<T>(T socket) where T : ISocketPollable, IDisposable;
    }
}