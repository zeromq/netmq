using System;

namespace NetMQ
{
    /// <summary>
    /// This enum-type represents the various numeric socket-related error codes.
    /// </summary>
    public enum ErrorCode
    {
        EndpointNotFound = 2,
        AddressAlreadyInUse = 48,
        TryAgain = 35,
        AccessDenied = 13,
        Invalid = 22,
        InProgres = 36,
        ProtocolNotSupported = 43,
        Fault = 14,
        AddressNotAvailable = 49,
        NetworkDown = 50,
        NoBufferSpaceAvailable = 55,
        NotConnected = 57,
        ConnectionRefused = 61,
        HostUnreachable = 65,

        BaseErrorNumber = 156384712,

        MessageSize = BaseErrorNumber + 10,
        AddressFamilyNotSupported = BaseErrorNumber + 11,
        NetworkUnreachable = BaseErrorNumber + 12,

        ConnectionAborted = BaseErrorNumber + 13,
        ConnectionReset = BaseErrorNumber + 14,
        TimedOut = BaseErrorNumber + 16,
        NetworkReset = BaseErrorNumber + 18,

        FiniteStateMachine = BaseErrorNumber + 51,
        ContextTerminated = BaseErrorNumber + 53,
        EmptyThread = BaseErrorNumber + 54,
        TooManyOpenSockets = BaseErrorNumber + 107,

        [Obsolete("Use EndpointNotFound")]
        ENOENT = 2,

        [Obsolete("Not in use")]
        EINTR = 4,

        [Obsolete("Use AccessDenied")]
        EACCESS = 13,

        [Obsolete("Use Fault")]
        EFAULT = 14,

        [Obsolete("Use Invalid")]
        EINVAL = 22,

        [Obsolete("Use TryAgain")]
        EAGAIN = 35,

        [Obsolete("Use InProgres")]
        EINPROGRESS = 36,

        [Obsolete("Use ProtocolNotSupported")]
        EPROTONOSUPPORT = 43,

        [Obsolete("Not in use")]
        ENOTSUP = 45,

        [Obsolete("Use AddressAlreadyInUse")]
        EADDRINUSE = 48,

        [Obsolete("Use AddressNotAvailable")]
        EADDRNOTAVAIL = 49,

        [Obsolete("Use NetworkDown")]
        ENETDOWN = 50,

        [Obsolete("Use NoBufferSpaceAvailable")]
        ENOBUFS = 55,

        [Obsolete("Not in use")]
        EISCONN = 56,

        [Obsolete("Use NotConnected")]
        ENOTCONN = 57,

        [Obsolete("Use ConnectionRefused")]
        ECONNREFUSED = 61,

        [Obsolete("Use HostUnreachable")]
        EHOSTUNREACH = 65,

        [Obsolete("Use BaseErrorNumber")]
        ZMQ_HAUSNUMERO = BaseErrorNumber,

        [Obsolete("Not is use")]
        ENOTSOCK = BaseErrorNumber + 9,

        [Obsolete("Use MessageSize")]
        EMSGSIZE = BaseErrorNumber + 10,

        [Obsolete("Use AddressFamilyNotSupported")]
        EAFNOSUPPORT = BaseErrorNumber + 11,

        [Obsolete("Use NetworkUnreachable")]
        ENETUNREACH = BaseErrorNumber + 12,

        [Obsolete("Use ConnectionAborted")]
        ECONNABORTED = BaseErrorNumber + 13,

        [Obsolete("Use ConnectionReset")]
        ECONNRESET = BaseErrorNumber + 14,

        [Obsolete("Use TimedOut")]
        ETIMEDOUT = BaseErrorNumber + 16,

        [Obsolete("Use NetworkReset")]
        ENETRESET = BaseErrorNumber + 18,        

        [Obsolete("Use FiniteStateMachine")]
        EFSM = BaseErrorNumber + 51,

        [Obsolete("Not in use")]
        ENOCOMPATPROTO = BaseErrorNumber + 52,

        [Obsolete("Use ContextTerminated")]
        ETERM = BaseErrorNumber + 53,

        [Obsolete("Use EmptyThread")]
        EMTHREAD = BaseErrorNumber + 54,

        [Obsolete("Not Is Use")]
        EIOEXC = BaseErrorNumber + 105,

        [Obsolete]
        ESOCKET = BaseErrorNumber + 106,

        [Obsolete("Use TooManyOpenSockets")]
        EMFILE = BaseErrorNumber + 107
    }
}