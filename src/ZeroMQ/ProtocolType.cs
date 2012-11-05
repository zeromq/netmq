namespace ZeroMQ
{
    /// <summary>
    /// Specifies the protocols that an <see cref="ZmqSocket"/> supports.
    /// </summary>
    public enum ProtocolType
    {
        /// <summary>
        /// Both Internet Protocol versions 4 and 6.
        /// </summary>
        Both = 0,

        /// <summary>
        /// Internet Protocol version 4.
        /// </summary>
        Ipv4Only = 1
    }
}
