using System;

namespace NetMQ
{
    /// <summary>
    /// This enum-type specifies socket transport events (TCP and IPC only).
    /// </summary>
    [Flags]
    public enum SocketEvents
    {
        /// <summary>
        /// </summary>
        Connected = 1,
        /// <summary>
        /// </summary>
        ConnectDelayed = 2,
        /// <summary>
        /// </summary>
        ConnectRetried = 4,

        /// <summary>
        /// </summary>
        Listening = 8,
        /// <summary>
        /// </summary>
        BindFailed = 16,

        /// <summary>
        /// </summary>
        Accepted = 32,
        /// <summary>
        /// </summary>
        AcceptFailed = 64,

        /// <summary>
        /// </summary>
        Closed = 128,
        /// <summary>
        /// </summary>
        CloseFailed = 256,
        /// <summary>
        /// </summary>
        Disconnected = 512,

        /// <summary>
        /// </summary>
        All = Connected | ConnectDelayed |
              ConnectRetried | Listening |
              BindFailed | Accepted |
              AcceptFailed | Closed |
              CloseFailed | Disconnected
    }
}