namespace ZeroMQ
{
    /// <summary>
    /// Specifies <see cref="SocketType.ROUTER"/> socket behavior when
    /// an unroutable message is encountered.
    /// </summary>
    public enum RouterBehavior
    {
        /// <summary>
        /// Silently discard messages.
        /// </summary>
        Discard = 0,

        /// <summary>
        /// Force sending to fail with an 'EAGAIN' error code, effectively
        /// enabling blocking sends.
        /// </summary>
        Report = 1,
    }
}