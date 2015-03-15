using System;

namespace NetMQ.zmq
{
    /// <summary>
    /// This enum-type serves to identity a particular socket-option.
    /// </summary>
    internal enum ZmqSocketOption
    {
        /// <summary>
        /// The I/O-thread affinity. This is a 64-bit value used to specify  which threads from the I/O thread-pool
        /// associated with the socket's context shall handle newly-created connections.
        /// 0 means no affinity, meaning that work shall be distributed fairly among all I/O threads.
        /// For non-zero values, the lowest bit corresponds to thread 1, second lowest bit to thread 2, and so on.
        /// </summary>
        Affinity = 4,

        Identity = 5,
        Subscribe = 6,
        Unsubscribe = 7,
        Rate = 8,
        RecoveryIvl = 9,
        SendBuffer = 11,
        ReceiveBuffer = 12,
        ReceiveMore = 13,

        Handle = 14,

        Events = 15,
        Type = 16,
        Linger = 17,

        /// <summary>
        /// The reconnect-interval.
        /// </summary>
        ReconnectIvl = 18,

        Backlog = 19,

        /// <summary>
        /// The maximum reconnect-interval.
        /// </summary>
        ReconnectIvlMax = 21,

        /// <summary>
        /// The upper limit to how many bytes a message may have.
        /// </summary>
        MaxMessageSize = 22,

        /// <summary>
        /// The high-water mark for message transmission, which is the number of messages that are allowed to queue up
        /// before mitigative action is taken. The default value is 1000.
        /// </summary>
        SendHighWatermark = 23,

        /// <summary>
        /// The high-water mark for message reception, which is the number of messages that are allowed to queue up
        /// before mitigative action is taken. The default value is 1000.
        /// </summary>
        ReceiveHighWatermark = 24,

        MulticastHops = 25,

        /// <summary>
        /// Specifies the amount of time after which a synchronous receive call will time out.
        /// </summary>
        [Obsolete("Pass a TimeSpan value directly to socket receive methods instead.")]
        ReceiveTimeout = 27,

        /// <summary>
        /// Specifies the amount of time after which a synchronous send call will time out.
        /// </summary>
        SendTimeout = 28,

        IPv4Only = 31,
        LastEndpoint = 32,
        RouterMandatory = 33,
        TcpKeepalive = 34,
        TcpKeepaliveIdle = 36,
        TcpKeepaliveIntvl = 37,
        TcpAcceptFilter = 38,
        DelayAttachOnConnect = 39,
        XpubVerbose = 40,
        RouterRawSocket = 41,
        XPublisherManual = 42,
        XPublisherWelcomeMessage = 43,

        /// <summary>
        /// Specifies the byte-order: big-endian, vs little-endian.
        /// </summary>
        Endian = 1000
    }
}