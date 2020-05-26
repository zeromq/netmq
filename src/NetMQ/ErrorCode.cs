namespace NetMQ
{
    /// <summary>
    /// This enum-type represents the various numeric socket-related error codes.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// The provided endpoint is not connected.
        /// </summary>
        EndpointNotFound = 2,

        /// <summary>
        /// The requested address is already in use.
        /// </summary>
        AddressAlreadyInUse = 48,

        /// <summary>
        /// Non-blocking mode was requested and the message cannot be sent at the moment.
        /// </summary>
        TryAgain = 35,

        /// <summary>
        /// Permission denied
        /// </summary>
        AccessDenied = 13,

        /// <summary>
        /// The endpoint supplied is invalid.
        /// </summary>
        Invalid = 22,

        /// <summary>
        /// The connection is still in progress.
        /// </summary>
        InProgress = 36,

        /// <summary>
        /// The requested transport protocol is not supported.
        /// </summary>
        ProtocolNotSupported = 43,

        /// <summary>
        /// The provided context is invalid.
        /// </summary>
        Fault = 14,

        /// <summary>
        /// The requested address was not available.
        /// For Bind operations, that can mean the address was not local.
        /// </summary>
        AddressNotAvailable = 49,

        /// <summary>
        /// The network appears to be down.
        /// </summary>
        NetworkDown = 50,

        /// <summary>
        /// There is not enough buffer space for the requested operation.
        /// </summary>
        NoBufferSpaceAvailable = 55,

        /// <summary>
        /// The socket is not connected.
        /// </summary>
        NotConnected = 57,

        /// <summary>
        /// The connection was refused.
        /// </summary>
        ConnectionRefused = 61,

        /// <summary>
        /// The host is not reachable.
        /// </summary>
        HostUnreachable = 65,

        /// <summary>
        /// This is the value chosen for beginning the range of 0MQ error codes.
        /// </summary>
        BaseErrorNumber = 156384712,

        /// <summary>
        /// The message is too long.
        /// </summary>
        MessageSize = BaseErrorNumber + 10,

        /// <summary>
        /// The address family is not supported by this protocol.
        /// </summary>
        AddressFamilyNotSupported = BaseErrorNumber + 11,

        /// <summary>
        /// The network is apparently not reachable.
        /// </summary>
        NetworkUnreachable = BaseErrorNumber + 12,

        /// <summary>
        /// The connection-attempt has apparently been aborted.
        /// </summary>
        ConnectionAborted = BaseErrorNumber + 13,

        /// <summary>
        /// The connection has apparently been reset.
        /// </summary>
        ConnectionReset = BaseErrorNumber + 14,

        /// <summary>
        /// The operation timed-out.
        /// </summary>
        TimedOut = BaseErrorNumber + 16,

        /// <summary>
        /// The connection has apparently been reset.
        /// </summary>
        NetworkReset = BaseErrorNumber + 18,

        /// <summary>
        /// The operation cannot be performed on this socket at the moment due
        /// to the socket not being in the appropriate state.
        /// </summary>
        FiniteStateMachine = BaseErrorNumber + 51,

        /// <summary>
        /// The context associated with the specified socket has already been terminated.
        /// </summary>
        ContextTerminated = BaseErrorNumber + 53,

        /// <summary>
        /// No I/O thread is available to accomplish this task.
        /// </summary>
        EmptyThread = BaseErrorNumber + 54,

        /// <summary>
        /// Too many sockets for this process.
        /// </summary>
        TooManyOpenSockets = BaseErrorNumber + 107
    }
}