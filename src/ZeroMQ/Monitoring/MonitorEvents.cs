namespace ZeroMQ.Monitoring
{
    using System;

    /// <summary>
    /// Socket transport events (for TCP and IPC sockets) that can be monitored.
    /// </summary>
    [Flags]
    public enum MonitorEvents
    {
        /// <summary>
        /// Triggered when a connection has been established to a remote peer. This can happen either synchronously or asynchronously.
        /// </summary>
        /// <remarks>
        /// NOTE: Do not rely on the 'addr' value for 'Connected' messages, as the memory
        /// address contained in the message may no longer point to the correct value.
        /// </remarks>
        Connected = 1,

        /// <summary>
        /// Triggered when an immediate connection attempt is delayed and it's completion is being polled for.
        /// </summary>
        ConnectDelayed = 2,

        /// <summary>
        /// Triggered when a connection attempt is being handled by reconnect timer. The reconnect interval is recomputed for each attempt.
        /// </summary>
        ConnectRetried = 4,

        /// <summary>
        /// Triggered when a socket is successfully bound to a an interface.
        /// </summary>
        Listening = 8,

        /// <summary>
        /// Triggered when a socket could not bind to a given interface.
        /// </summary>
        BindFailed = 16,

        /// <summary>
        /// Triggered when a connection from a remote peer has been established with a socket's listen address.
        /// </summary>
        Accepted = 32,

        /// <summary>
        /// Triggered when a connection attempt to a socket's bound address fails.
        /// </summary>
        AcceptFailed = 64,

        /// <summary>
        /// Triggered when a connection's underlying descriptor has been closed.
        /// </summary>
        /// <remarks>
        /// NOTE: Do not rely on the 'addr' value for 'Closed' messages, as the memory
        /// address contained in the message may no longer point to the correct value.
        /// </remarks>
        Closed = 128,

        /// <summary>
        /// Triggered when a descriptor could not be released back to the OS.
        /// </summary>
        CloseFailed = 256,

        /// <summary>
        /// Triggered when the stream engine (tcp and ipc specific) detects a corrupted / broken session.
        /// </summary>
        Disconnected = 512,

        /// <summary>
        /// A bitwise combination of all <see cref="MonitorEvents"/> values.
        /// </summary>
        AllEvents = Connected | ConnectDelayed | ConnectRetried | Listening | BindFailed |
                    Accepted | AcceptFailed | Closed | CloseFailed | Disconnected
    }
}