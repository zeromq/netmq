using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// Interface ISocketPollable mandates a Socket-get ( NetMQSocket ) property.
    /// </summary>
    public interface ISocketPollable
    {
        /// <summary>
        /// Get the NetMQSocket.
        /// </summary>
        [NotNull]
        NetMQSocket Socket { get; }
    }
}
