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
    public interface ISocketPollableCollection
    {
        void Add([NotNull] ISocketPollable socket);
        void Remove([NotNull] ISocketPollable socket);
    }
}