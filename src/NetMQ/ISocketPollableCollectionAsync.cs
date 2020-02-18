using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// This interface provides an abstraction over the legacy Poller and newer <see cref="NetMQPoller"/> classes for use in <see cref="NetMQMonitor"/> and avoids thread sync issues removing sockets.
    /// </remarks>
    public interface ISocketPollableCollectionAsync
    {
        Task RemoveAsync([NotNull] ISocketPollable socket);
        Task RemoveAndDisposeAsync<T>(T socket) where T : ISocketPollable, IDisposable;
    }
}
