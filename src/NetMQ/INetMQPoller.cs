using System;

namespace NetMQ
{
    /// <summary>
    /// Interface to of the NetMQPoller, implement to fake the NetMQPoller in tests.
    /// </summary>
    public interface INetMQPoller : IDisposable
    {
        /// <summary>
        /// Runs the poller on the caller's thread. Only returns when <see cref="Stop"/> or <see cref="StopAsync"/> are called from another thread.
        /// </summary>
        void Run();
        
        /// <summary>
        /// Runs the poller in a background thread, returning once the poller has started.
        /// </summary>
        void RunAsync();
        
        /// <summary>
        /// Stops the poller.
        /// </summary>
        /// <remarks>
        /// If called from a thread other than the poller thread, this method will block until the poller has stopped.
        /// If called from the poller thread it is not possible to block.
        /// </remarks>
        void Stop();
        
        /// <summary>
        /// Stops the poller, returning immediately and most likely before the poller has actually stopped.
        /// </summary>
        void StopAsync();

        /// <summary>
        /// Get whether this object is currently polling its sockets and timers.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Add a socket to the poller
        /// </summary>
        /// <param name="socket">Socket to add to the poller</param>
        void Add(ISocketPollable socket);

        /// <summary>
        /// Remove a socket from the poller
        /// </summary>
        /// <param name="socket">The socket to be removed</param>
        void Remove(ISocketPollable socket);

        /// <summary>
        /// Remove the socket from the poller and dispose the socket
        /// </summary>
        /// <param name="socket">The socket to be removed</param>
        void RemoveAndDispose<T>(T socket) where T : ISocketPollable, IDisposable;
    }
}
