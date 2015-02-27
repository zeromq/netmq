using System;

namespace NetMQ.zmq
{
    /// <summary>
    /// This enum-type is either IOThreads (1) or MaxSockets (2).
    /// </summary>
    public enum ContextOption
    {
        IOThreads = 1,
        MaxSockets = 2
    }

    /// <summary>
    /// This enum-type is used to specify the basic type of message-queue socket
    /// based upon the intended pattern, such as Pub,Sub, Req,Rep, Dealer,Router, Pull,Push, Xpub,Xsub.
    /// </summary>
    public enum ZmqSocketType
    {
        None = -1,
        Pair = 0,

        /// <summary>
        /// This denotes a Publisher socket (usually paired with a Subscriber socket).
        /// </summary>
        Pub = 1,

        /// <summary>
        /// This denotes a Subscriber socket (usually paired with a Publisher socket).
        /// </summary>
        Sub = 2,

        /// <summary>
        /// This denotes a Request socket (usually paired with a Response socket).
        /// </summary>
        Req = 3,

        /// <summary>
        /// This denotes a Reponse socket (usually paired with a Request socket).
        /// </summary>
        Rep = 4,

        Dealer = 5,
        Router = 6,

        /// <summary>
        /// This denotes a Pull socket (usually paired with a PUsh socket).
        /// </summary>
        Pull = 7,

        /// <summary>
        /// This denotes a Push socket (usually paired with a Pull socket).
        /// </summary>
        Push = 8,

        Xpub = 9,
        Xsub = 10,
        Stream = 11
    }

    /// <summary>
    /// This enum-type serves to identity a particular socket-option.
    /// </summary>
    public enum ZmqSocketOptions
    {
        Affinity = 4,
        Identity = 5,
        Subscribe = 6,
        Unsubscribe = 7,
        Rate = 8,
        RecoveryIvl = 9,
        SendBuffer = 11,
        [Obsolete("Use ReceiveBuffer instead")]
        ReceivevBuffer = ReceiveBuffer,
        ReceiveBuffer = 12,
        ReceiveMore = 13,
        
        [Obsolete("Use Handle")]
        FD = 14,
        Handle = 14,

        Events = 15,
        Type = 16,
        Linger = 17,
        ReconnectIvl = 18,
        Backlog = 19,
        ReconnectIvlMax = 21,
        Maxmsgsize = 22,
        SendHighWatermark = 23,
        [Obsolete("Use ReceiveHighWatermark instead")]
        ReceivevHighWatermark = ReceiveHighWatermark,
        ReceiveHighWatermark = 24,
        MulticastHops = 25,
        ReceiveTimeout = 27,
        SendTimeout = 28,
        IPv4Only = 31,
        LastEndpoint = 32,
        RouterMandatory = 33,
        TcpKeepalive = 34,
        [Obsolete("Not supported and has no effect")]
        TcpKeepaliveCnt = 35,
        TcpKeepaliveIdle = 36,
        TcpKeepaliveIntvl = 37,
        TcpAcceptFilter = 38,
        DelayAttachOnConnect = 39,
        XpubVerbose = 40,
        RouterRawSocket = 41,
        XPublisherManual = 42,
        XPublisherWelcomeMessage = 43,

        Endian = 1000,

        [Obsolete]
        FailUnroutable = RouterMandatory,

        [Obsolete]
        RouterBehavior = RouterMandatory
    }

    /// <summary>
    /// This enum-type specifies either big-endian (Big) or little-endian (Little),
    /// which indicate whether the most-significant bits are placed first or last in memory.
    /// </summary>
    public enum Endianness
    {
        Big,
        Little
    }

    /// <summary>
    /// This flags enum-type provides a way to specify basic Receive behavior.
    /// It may be None, or have the DontWait bit set (indicating to wait for a message,
    /// or the SendMore bit.
    /// </summary>
    [Flags]
    public enum SendReceiveOptions
    {
        /// <summary>
        /// Clear all flags (set neither DontWait nor SendMore).
        /// </summary>
        None = 0,

        /// <summary>
        /// Set this flag to specify NOT to block waiting for a message to arrive.
        /// </summary>
        DontWait = 1,

        /// <summary>
        /// Set this flag if you want to set the SendMore bit.
        /// </summary>
        SendMore = 2,

        // Deprecated aliases
        [Obsolete("Use DontWait instead")]
        NoBlock = DontWait
    }

    // Socket transport events (tcp and ipc only)

    /// <summary>
    /// This enum-type specifies socket transport events (TCP and IPC only).
    /// </summary>
    [Flags]
    public enum SocketEvent
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

    /// <summary>
    /// This flags enum-type is simply an indication of the direction of the poll-event,
    /// and can be None, PollIn, PollOut, or PollError.
    /// </summary>
    [Flags]
    public enum PollEvents
    {
        None = 0x0,
        PollIn = 0x1,
        PollOut = 0x2,
        PollError = 0x4
    }
}