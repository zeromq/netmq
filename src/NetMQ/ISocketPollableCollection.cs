using System;
using JetBrains.Annotations;
using NetMQ.Monitoring;

namespace NetMQ
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// This interface provides an abstraction over the legacy Poller and newer <see cref="NetMQPoller"/> classes for use in <see cref="NetMQMonitor"/>.
    /// </remarks>
    [Obsolete("this should be made internal, to avoid amjor re-work of NetMQMonitor, but prevent accidental mis-use from applications")]
    public interface ISocketPollableCollection
    {
        void Add([NotNull] ISocketPollable socket);
        void Remove([NotNull] ISocketPollable socket);
        void RemoveAndDispose<T>(T socket) where T : ISocketPollable, IDisposable;
    }
}