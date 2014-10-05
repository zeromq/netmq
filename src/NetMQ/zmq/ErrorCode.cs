using System;

namespace NetMQ.zmq
{
    public enum ErrorCode
    {
        ENOENT = 2,
        EINTR = 4,
        EACCESS = 13,
        EFAULT = 14,
        EINVAL = 22,
        EAGAIN = 35,
        EINPROGRESS = 36,
        EPROTONOSUPPORT = 43,
        ENOTSUP = 45,
        EADDRINUSE = 48,
        EADDRNOTAVAIL = 49,
        ENETDOWN = 50,
        ENOBUFS = 55,
        EISCONN = 56,
        ENOTCONN = 57,
        ECONNREFUSED = 61,
        EHOSTUNREACH = 65,


        ZMQ_HAUSNUMERO = 156384712,

        ENOTSOCK = ZMQ_HAUSNUMERO + 9,
        EMSGSIZE = ZMQ_HAUSNUMERO + 10,
        EAFNOSUPPORT = ZMQ_HAUSNUMERO + 11,
        ENETUNREACH = ZMQ_HAUSNUMERO + 12,
        ECONNABORTED = ZMQ_HAUSNUMERO + 13,
        ECONNRESET = ZMQ_HAUSNUMERO + 14,
        ETIMEDOUT = ZMQ_HAUSNUMERO + 16,
        ENETRESET = ZMQ_HAUSNUMERO + 18,


        EFSM = ZMQ_HAUSNUMERO + 51,
        ENOCOMPATPROTO = ZMQ_HAUSNUMERO + 52,
        ETERM = ZMQ_HAUSNUMERO + 53,
        EMTHREAD = ZMQ_HAUSNUMERO + 54,

        EIOEXC = ZMQ_HAUSNUMERO + 105,
        ESOCKET = ZMQ_HAUSNUMERO + 106,
        EMFILE = ZMQ_HAUSNUMERO + 107
    }
}