using System;

namespace NetMQ
{
    /// <summary>
    /// This enum-type specifies socket transport events (TCP and IPC only).
    /// </summary>
    [Flags]
    public enum SocketEvents
    {
        Connected = 1,
        ConnectDelayed = 2,
        ConnectRetried = 4,

        Listening = 8,
        BindFailed = 16,

        Accepted = 32,
        AcceptFailed = 64,

        Closed = 128,
        CloseFailed = 256,
        Disconnected = 512,

        All = Connected | ConnectDelayed |
              ConnectRetried | Listening |
              BindFailed | Accepted |
              AcceptFailed | Closed |
              CloseFailed | Disconnected
    }
}