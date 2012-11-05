namespace ZeroMQ
{
    using System;

    /// <summary>
    /// Flags used by socket Send and Receive operations.
    /// </summary>
    [Flags]
    public enum SocketFlags
    {
        /// <summary>
        /// No socket flags are specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The operation should be performed in non-blocking mode.
        /// </summary>
        DontWait = 0x1,

        /// <summary>
        /// The message being sent is a multi-part message, and that further message parts are to follow.
        /// </summary>
        SendMore = 0x2,
    }
}
