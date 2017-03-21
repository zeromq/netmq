using System;

namespace NetMQ.Core
{
    /// <summary>
    /// This enum-type serves to identity a particular socket-option.
    /// </summary>
    internal enum ZmqSocketOption
    {
        /// <summary>
        /// The I/O-thread affinity.
        /// 0 means no affinity, meaning that work shall be distributed fairly among all I/O threads.
        /// For non-zero values, the lowest bit corresponds to thread 1, second lowest bit to thread 2, and so on.
        /// </summary>
        /// <remarks>
        /// The I/O-thread <c>Affinity</c> is a 64-bit value used to specify which threads from the I/O thread-pool
        /// associated with the socket's context shall handle newly-created connections.
        /// 0 means no affinity, meaning that work shall be distributed fairly among all I/O threads.
        /// For non-zero values, the lowest bit corresponds to thread 1, second lowest bit to thread 2, and so on.
        /// </remarks>
        Affinity = 4,

        /// <summary>
        /// The unique identity of the socket, from a message-queueing router's perspective.
        /// </summary>
        /// <remarks>
        /// This is at most 255 bytes long.
        /// </remarks>
        Identity = 5,

        /// <summary>
        /// Setting this option subscribes a socket to messages that have the given topic. This is valid only for Subscriber and XSubscriber sockets.
        /// </summary>
        /// <remarks>
        /// You subscribe a socket to a given topic when you want that socket to receive messages of that topic.
        /// A topic is simply a specific prefix (in the form of a byte-array or the equivalent text).
        /// This is valid only for Subscriber and XSubscriber sockets.
        /// </remarks>
        Subscribe = 6,

        /// <summary>
        /// Set this option to un-subscribe a socket from a given topic. Only for Subscriber and XSubscriber sockets.
        /// </summary>
        /// <remarks>
        /// You un-subscribe a socket from the given topic when you no longer want that socket to receive
        /// messages of that topic. A topic is simply a specific prefix (in the form of a byte-array or the equivalent text).
        /// This is valid only for Subscriber and XSubscriber sockets.
        /// </remarks>
        Unsubscribe = 7,

        /// <summary>
        /// The maximum send or receive data rate for multicast transports on the specified socket.
        /// </summary>
        Rate = 8,

        /// <summary>
        /// The recovery-interval, in milliseconds, for multicast transports using the specified socket.
        /// Default is 10,000 ms (10 seconds).
        /// </summary>
        /// <remarks>
        /// This option determines the maximum time that a receiver can be absent from a multicast group
        /// before unrecoverable data loss will occur. Default is 10,000 ms (10 seconds).
        /// </remarks>
        RecoveryIvl = 9,

        /// <summary>
        /// The size of the transmit buffer for the specified socket.
        /// </summary>
        SendBuffer = 11,

        /// <summary>
        /// The size of the receive buffer for the specified socket.
        /// </summary>
        ReceiveBuffer = 12,

        /// <summary>
        /// This indicates more messages are to be received.
        /// </summary>
        ReceiveMore = 13,

        /// <summary>
        /// The file descriptor associated with the specified socket.
        /// </summary>
        Handle = 14,

        /// <summary>
        /// The event state for the specified socket.
        /// This is a combination of:
        ///   PollIn - at least one message may be received without blocking
        ///   PollOut - at least one messsage may be sent without blocking
        /// </summary>
        Events = 15,

        Type = 16,

        /// <summary>
        /// This option specifies the linger period for the specified socket,
        /// which determines how long pending messages which have yet to be sent to a peer
        /// shall linger in memory after a socket is closed.
        /// </summary>
        Linger = 17,

        /// <summary>
        /// The initial reconnection interval for the specified socket.
        /// -1 means no reconnection.
        /// </summary>
        /// <remarks>
        /// This is the period to wait between attempts to reconnect disconnected peers
        /// when using connection-oriented transports.
        /// A value of -1 means no reconnection.
        /// </remarks>
        ReconnectIvl = 18,

        /// <summary>
        /// This is the maximum length of the queue of outstanding peer connections
        /// for the specified socket. This only applies to connection-oriented transports.
        /// Default is 100.
        /// </summary>
        Backlog = 19,

        /// <summary>
        /// The maximum reconnection interval for the specified socket.
        /// The default value of zero means no exponential backoff is performed.
        /// </summary>
        /// <remarks>
        /// This option value denotes the maximum reconnection interval for a socket.
        /// It is used when a connection drops and NetMQ attempts to reconnect.
        /// On each attempt to reconnect, the previous interval is doubled
        /// until this maximum period is reached.
        /// The default value of zero means no exponential backoff is performed.
        /// </remarks>
        ReconnectIvlMax = 21,

        /// <summary>
        /// The upper limit to the size for inbound messages.
        /// -1 (the default value) means no limit.
        /// </summary>
        /// <remarks>
        /// If a peer sends a message larger than this it is disconnected.
        /// </remarks>
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

        /// <summary>
        /// The time-to-live (maximum number of hops) that outbound multicast packets
        /// are allowed to propagate.
        /// The default value of 1 means that the multicast packets don't leave the local network.
        /// </summary>
        MulticastHops = 25,

        /// <summary>
        /// Specifies the amount of time after which a synchronous send call will time out.
        /// A value of 0 means Send will return immediately, with a EAGAIN error if the message cannot be sent.
        /// -1 means to block until the message is sent.
        /// </summary>
        SendTimeout = 28,

        /// <summary>
        /// This indicates the underlying native socket type.
        /// </summary>
        /// <remarks>
        /// An IPv4 socket will only use IPv4, while an IPv6 socket lets applications
        /// connect to and accept connections from both IPv4 and IPv6 hosts.
        /// </remarks>
        IPv4Only = 31,

        /// <summary>
        /// The last endpoint bound for TCP and IPC transports.
        /// The returned value will be a string in the form of a ZMQ DSN.
        /// </summary>
        /// <remarks>
        /// If the TCP host is ANY, indicated by a *, then the returned address
        /// will be 0.0.0.0 (for IPv4).
        /// </remarks>
        LastEndpoint = 32,

        /// <summary>
        /// Sets the RouterSocket behavior when an unroutable message is encountered.
        /// A value of 0 is the default and discards the message silently when it cannot be routed.
        /// A value of 1 returns an EHOSTUNREACH error code if the message cannot be routed.
        /// </summary>
        RouterMandatory = 33,

        /// <summary>
        /// Whether to use TCP keep-alive on this socket.
        /// 0 = no, 1 = yes,
        /// -1 (the default value) means to skip any overrides and leave it to the OS default.
        /// </summary>
        TcpKeepalive = 34,

        /// <summary>
        /// The keep-alive time - the duration between two keepalive transmissions in idle condition.
        /// </summary>
        /// <remarks>
        /// The TCP keepalive period is required by socket implementers to be configurable and by default is
        /// set to no less than 2 hours.
        /// In 0MQ, -1 (the default value) means to just leave it to the OS default.
        /// </remarks>
        TcpKeepaliveIdle = 36,

        /// <summary>
        /// The TCP keep-alive interval - the duration between two keepalive transmission if no response was received to a previous keepalive probe.
        /// </summary>
        /// <remarks>
        /// By default a keepalive packet is sent every 2 hours or 7,200,000 milliseconds
        /// if no other data have been carried over the TCP connection.
        /// If there is no response to a keepalive, it is repeated once every KeepAliveInterval seconds.
        /// The default is one second.
        /// </remarks>
        TcpKeepaliveIntvl = 37,

        /// <summary>
        /// The list of accept-filters, which denote the addresses that a socket may accept.
        /// Setting this to null clears the filter.
        /// </summary>
        /// <remarks>
        /// This applies to IPv4 addresses only.
        /// </remarks>
        DelayAttachOnConnect = 39,

        /// <summary>
        /// This applies only to XPub sockets.
        /// If true, send all subscription messages upstream, not just unique ones.
        /// The default is false.
        /// </summary>
        XpubVerbose = 40,

        /// <summary>
        /// If true, router socket accepts non-zmq tcp connections
        /// </summary>
        RouterRawSocket = 41,

        XPublisherManual = 42,

        /// <summary>
        /// This is an XPublisher-socket welcome-message.
        /// </summary>
        XPublisherWelcomeMessage = 43,


        DisableTimeWait = 44,

        /// <summary>
        /// This applies only to XPub sockets.
        /// If true, enable broadcast option on XPublishers
        /// </summary>
        XPublisherBroadcast = 45,

        /// <summary>
        /// The low-water mark for message transmission. This is the number of messages that should be processed
        /// before transmission is unblocked (in case it was blocked by reaching high-watermark). The default value is
        /// calculated using relevant high-watermark (HWM): HWM > 2048 ? HWM - 1024 : (HWM + 1) / 2
        /// </summary>
        SendLowWatermark = 46,

        /// <summary>
        /// The low-water mark for message reception. This is the number of messages that should be processed
        /// before reception is unblocked (in case it was blocked by reaching high-watermark). The default value is
        /// calculated using relevant high-watermark (HWM): HWM > 2048 ? HWM - 1024 : (HWM + 1) / 2
        /// </summary>
        ReceiveLowWatermark = 47,

        /// <summary>
        /// When enabled new router connections with same identity take over old ones
        /// </summary>
        RouterHandover = 48,

        /// <summary>
        /// Specifies the byte-order: big-endian, vs little-endian.
        /// </summary>
        Endian = 1000,

        /// <summary>
        /// Specifies the max datagram size for PGM.
        /// </summary>
        PgmMaxTransportServiceDataUnitLength = 1001
    }
}