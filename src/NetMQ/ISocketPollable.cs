using JetBrains.Annotations;

namespace NetMQ
{
    public interface ISocketPollable
    {
        [NotNull]
        NetMQSocket Socket { get; }
    }
}
