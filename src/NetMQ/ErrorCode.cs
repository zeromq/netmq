using System;

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
        /// The connection is still in progress.
        /// </summary>
        [Obsolete("Use InProgress")]
        InProgres = 36,

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
        TooManyOpenSockets = BaseErrorNumber + 107,

        /// <summary>
        /// The provided endpoint is not connected.
        /// </summary>
        [Obsolete("Use EndpointNotFound")]
        ENOENT = 2,

        /// <summary>
        /// The operation was interrupted by a signal.
        /// </summary>
        [Obsolete("Not in use")]
        EINTR = 4,

        /// <summary>
        /// Permission denied
        /// </summary>
        [Obsolete("Use AccessDenied")]
        EACCESS = 13,

        /// <summary>
        /// The provided context is invalid.
        /// </summary>
        [Obsolete("Use Fault")]
        EFAULT = 14,

        /// <summary>
        /// The endpoint supplied is invalid.
        /// </summary>
        [Obsolete("Use Invalid")]
        EINVAL = 22,

        /// <summary>
        /// Non-blocking mode was requested and the message cannot be sent at the moment.
        /// </summary>
        [Obsolete("Use TryAgain")]
        EAGAIN = 35,

        /// <summary>
        /// The connection is still in progress.
        /// </summary>
        [Obsolete("Use InProgress")]
        EINPROGRESS = 36,

        /// <summary>
        /// The requested transport protocol is not supported.
        /// </summary>
        [Obsolete("Use ProtocolNotSupported")]
        EPROTONOSUPPORT = 43,

        /// <summary>
        /// That operation is not supported by this socket type.
        /// </summary>
        [Obsolete("Not in use")]
        ENOTSUP = 45,

        /// <summary>
        /// The requested address is already in use.
        /// </summary>
        [Obsolete("Use AddressAlreadyInUse")]
        EADDRINUSE = 48,

        /// <summary>
        /// The requested address was not available.
        /// For Bind operations, that can mean the address was not local.
        /// </summary>
        [Obsolete("Use AddressNotAvailable")]
        EADDRNOTAVAIL = 49,

        /// <summary>
        /// The network appears to be down.
        /// </summary>
        [Obsolete("Use NetworkDown")]
        ENETDOWN = 50,

        /// <summary>
        /// There is not enough buffer space for the requested operation.
        /// </summary>
        [Obsolete("Use NoBufferSpaceAvailable")]
        ENOBUFS = 55,

        /// <summary>
        /// Unused
        /// </summary>
        [Obsolete("Not in use")]
        EISCONN = 56,

        /// <summary>
        /// The socket is not connected.
        /// </summary>
        [Obsolete("Use NotConnected")]
        ENOTCONN = 57,

        /// <summary>
        /// The connection was refused.
        /// </summary>
        [Obsolete("Use ConnectionRefused")]
        ECONNREFUSED = 61,

        /// <summary>
        /// The host is not reachable.
        /// </summary>
        [Obsolete("Use HostUnreachable")]
        EHOSTUNREACH = 65,

        /// <summary>
        /// This is the value chosen for beginning the range of 0MQ error codes.
        /// </summary>
        [Obsolete("Use BaseErrorNumber")]
        ZMQ_HAUSNUMERO = BaseErrorNumber,

        /// <summary>
        /// The provided socket was invalid.
        /// </summary>
        [Obsolete("Not in use")]
        ENOTSOCK = BaseErrorNumber + 9,

        /// <summary>
        /// The message is too long.
        /// </summary>
        [Obsolete("Use MessageSize")]
        EMSGSIZE = BaseErrorNumber + 10,

        /// <summary>
        /// The address family is not supported by this protocol.
        /// </summary>
        [Obsolete("Use AddressFamilyNotSupported")]
        EAFNOSUPPORT = BaseErrorNumber + 11,

        /// <summary>
        /// The network is apparently not reachable.
        /// </summary>
        [Obsolete("Use NetworkUnreachable")]
        ENETUNREACH = BaseErrorNumber + 12,

        /// <summary>
        /// The connection-attempt has apparently been aborted.
        /// </summary>
        [Obsolete("Use ConnectionAborted")]
        ECONNABORTED = BaseErrorNumber + 13,

        /// <summary>
        /// The connection has apparently been reset.
        /// </summary>
        [Obsolete("Use ConnectionReset")]
        ECONNRESET = BaseErrorNumber + 14,

        /// <summary>
        /// The operation timed-out.
        /// </summary>
        [Obsolete("Use TimedOut")]
        ETIMEDOUT = BaseErrorNumber + 16,

        /// <summary>
        /// The network was reset.
        /// </summary>
        [Obsolete("Use NetworkReset")]
        ENETRESET = BaseErrorNumber + 18,

        /// <summary>
        /// The operation cannot be performed on this socket at the moment due
        /// to the socket not being in the appropriate state.
        /// </summary>
        [Obsolete("Use FiniteStateMachine")]
        EFSM = BaseErrorNumber + 51,

        /// <summary>
        /// The requested transport protocol is not compatible with the socket type.
        /// </summary>
        [Obsolete("Not in use")]
        ENOCOMPATPROTO = BaseErrorNumber + 52,

        /// <summary>
        /// The context associated with the specified socket has already been terminated.
        /// </summary>
        [Obsolete("Use ContextTerminated")]
        ETERM = BaseErrorNumber + 53,

        /// <summary>
        /// No I/O thread is available to accomplish this task.
        /// </summary>
        [Obsolete("Use EmptyThread")]
        EMTHREAD = BaseErrorNumber + 54,

        /// <summary>
        /// Unused
        /// </summary>
        [Obsolete("Not In Use")]
        EIOEXC = BaseErrorNumber + 105,

        /// <summary>
        /// Unused
        /// </summary>
        [Obsolete]
        ESOCKET = BaseErrorNumber + 106,

        /// <summary>
        /// Too many sockets for this process.
        /// </summary>
        [Obsolete("Use TooManyOpenSockets")]
        EMFILE = BaseErrorNumber + 107
    }
}