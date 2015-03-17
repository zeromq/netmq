using System;


namespace NetMQ.zmq
{
    /// <summary>
    /// This enum-type is either IOThreads (1) or MaxSockets (2).
    /// </summary>
    internal enum ContextOption
    {
        IOThreads = 1,
        MaxSockets = 2
    }

    /// <summary>
    /// This enum-type is used to specify the basic type of message-queue socket
    /// based upon the intended pattern, such as Pub,Sub, Req,Rep, Dealer,Router, Pull,Push, XPub,XSub.
    /// </summary>
    public enum ZmqSocketType
    {
        /// <summary>
        /// No socket-type is specified
        /// </summary>
        None = -1,

        /// <summary>
        /// This denotes a Pair socket (usually paired with another Pair socket).
        /// </summary>
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
        /// This denotes a Response socket (usually paired with a Request socket).
        /// </summary>
        Rep = 4,

        /// <summary>
        /// This denotes an Dealer socket.
        /// </summary>
        Dealer = 5,

        /// <summary>
        /// This denotes an Router socket.
        /// </summary>
        Router = 6,

        /// <summary>
        /// This denotes a Pull socket (usually paired with a PUsh socket).
        /// </summary>
        Pull = 7,

        /// <summary>
        /// This denotes a Push socket (usually paired with a Pull socket).
        /// </summary>
        Push = 8,

        /// <summary>
        /// This denotes an XPublisher socket.
        /// </summary>
        Xpub = 9,

        /// <summary>
        /// This denotes an XSubscriber socket.
        /// </summary>
        Xsub = 10,

        /// <summary>
        /// This denotes a Stream socket - which is a parent-class to the other socket types.
        /// </summary>
        Stream = 11
    }

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
        [Obsolete("Use ReceiveBuffer instead")]
        ReceivevBuffer = ReceiveBuffer,

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
        /// The upper limit to to the size for inbound messages.
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
        /// Specifies the amount of time after which a synchronous receive call will time out.
        /// </summary>
        [Obsolete("Pass a TimeSpan value directly to socket receive methods instead.")]
        ReceiveTimeout = 27,

        /// <summary>
        /// Specifies the amount of time after which a synchronous send call will time out.
        /// A value of 0 means Send will return immediately, with a EAGAIN error if the message cannot be sent.
        /// -1 means to block until the message is sent.
        /// TODO: May need to update this explanation.
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
        /// (TODO: Check these comments concerning default values!  jh)
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
        TcpAcceptFilter = 38,

        /// <summary>
        /// The attach-on-connect value.
        /// Default is false.
        /// </summary>
        /// <remarks>
        /// If set to 1, will delay the attachment of a pipe on connect until
        /// the underlying connection has completed. This will cause the socket
        /// to block if there are no other connections, but will prevent queues
        /// from filling on pipes awaiting connection.
        /// </remarks>
        DelayAttachOnConnect = 39,

        XpubVerbose = 40,
        RouterRawSocket = 41,
        XPublisherManual = 42,

        /// <summary>
        /// This is an XPublisher-socket welcome-message.
        /// </summary>
        XPublisherWelcomeMessage = 43,

        /// <summary>
        /// Specifies the byte-order: big-endian, vs little-endian.
        /// </summary>
        Endian = 1000
    }

    /// <summary>
    /// This enum-type specifies either big-endian (Big) or little-endian (Little),
    /// which indicate whether the most-significant bits are placed first or last in memory.
    /// </summary>
    public enum Endianness
    {
        /// <summary>
        /// This means the most-significant bits are placed first in memory.
        /// </summary>
        Big,

        /// <summary>
        /// this means the most-significant bits are placed last in memory.
        /// </summary>
        Little
    }

    /// <summary>
    /// This flags enum-type provides a way to specify basic Receive behaviour.
    /// It may be None, or have the DontWait bit (indicating to wait for a message),
    /// or the SendMore bit, set.
    /// </summary>
    [Flags]
    public enum SendReceiveOptions
    {
        /// <summary>
        /// Both bits cleared (neither DontWait nor SendMore are set)
        /// </summary>
        None = 0,

        /// <summary>
        /// Set this flag to specify NOT to block waiting for a message to arrive.
        /// </summary>
        DontWait = 1,

        /// <summary>
        /// Set this (the SendMore bit) to signal more messages beyond the current one, for a given unit of communication.
        /// </summary>
        SendMore = 2,

        /// <summary>
        /// (Deprecated) This flag specifies NOT to block waiting for a message to arrive.
        /// </summary>
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
        /// <summary>
        /// This socket connected.
        /// </summary>
        Connected = 1,

        /// <summary>
        /// The socket was delayed from connecting.
        /// </summary>
        ConnectDelayed = 2,

        /// <summary>
        /// The connection was retried.
        /// </summary>
        ConnectRetried = 4,

        /// <summary>
        /// The socket has started listening for connections.
        /// </summary>
        Listening = 8,

        /// <summary>
        /// The socket bind operation failed.
        /// </summary>
        BindFailed = 16,

        /// <summary>
        /// The socket Accept operation succeeded.
        /// </summary>
        Accepted = 32,

        /// <summary>
        /// The socket Accept-call failed.
        /// </summary>
        AcceptFailed = 64,

        /// <summary>
        /// The socket was closed.
        /// </summary>
        Closed = 128,

        /// <summary>
        /// The close-operation on the socket failed.
        /// </summary>
        CloseFailed = 256,

        /// <summary>
        /// The socket has disconnected.
        /// </summary>
        Disconnected = 512,

        /// <summary>
        /// This is the bitwise-OR of all possible flag bits.
        /// </summary>
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
        /// <summary>
        /// Unknown - this is the default value.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// The poll-event indicates a message is ready to be received.
        /// </summary>
        PollIn = 0x1,
        /// <summary>
        /// The poll-event indicates a message is ready to be sent.
        /// </summary>
        PollOut = 0x2,
        /// <summary>
        /// The poll-event reflects a polling-error.
        /// </summary>
        PollError = 0x4
    }

    /// <summary>
    /// This static class provides the HasIn, HasOut, and HasError methods
    /// and exists as a convenience for determining these facts about a given PollEvents object.
    /// </summary>
    public static class PollEventsExtensions
    {
        /// <summary>
        /// Return true if the given <see cref="PollEvents"/> has the PollIn flag set.
        /// </summary>
        /// <param name="pollEvents">the PollEvents to check the flag of</param>
        /// <returns>true if the PollIn flag is set</returns>
        public static bool HasIn(this PollEvents pollEvents)
        {
            return (pollEvents & PollEvents.PollIn) == PollEvents.PollIn;
        }

        /// <summary>
        /// Return true if the given <see cref="PollEvents"/> has the PollOut flag set.
        /// </summary>
        /// <param name="pollEvents">the PollEvents to check the flag of</param>
        /// <returns>true if the PollOut flag is set</returns>
        public static bool HasOut(this PollEvents pollEvents)
        {
            return (pollEvents & PollEvents.PollOut) == PollEvents.PollOut;
        }

        /// <summary>
        /// Return true if the given <see cref="PollEvents"/> has the PollError flag set.
        /// </summary>
        /// <param name="pollEvents">the PollEvents to check the flag of</param>
        /// <returns>true if the PollError flag is set</returns>
        public static bool HasError(this PollEvents pollEvents)
        {
            return (pollEvents & PollEvents.PollError) == PollEvents.PollError;
        }
    }
}