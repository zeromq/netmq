namespace NetMQ
{
    /// <summary>
    /// Implementations provide a <see cref="NetMQSocket"/> via the <see cref="Socket"/> property.
    /// </summary>
    public interface ISocketPollable
    {
        /// <summary>
        /// Gets a <see cref="NetMQSocket"/> instance.
        /// </summary>
        NetMQSocket Socket { get; }

        /// <summary>
        /// Gets whether the object has been disposed.
        /// </summary>
        bool IsDisposed { get; }
    }
}
