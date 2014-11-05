using System;

namespace NetMQ.Core
{
    enum ZmqSocketOptions
    {
        Affinity = 4,
        Identity = 5,
        Subscribe = 6,
        Unsubscribe = 7,
        Rate = 8,
        RecoveryIvl = 9,
        SendBuffer = 11,
        ReceivevBuffer = 12,
        ReceiveMore = 13,
        FD = 14,
        Events = 15,
        Type = 16,
        Linger = 17,
        ReconnectIvl = 18,
        Backlog = 19,
        ReconnectIvlMax = 21,
        Maxmsgsize = 22,
        SendHighWatermark = 23,
        ReceivevHighWatermark = 24,
        MulticastHops = 25,
        ReceiveTimeout = 27,
        SendTimeout = 28,
        IPv4Only = 31,
        LastEndpoint = 32,
        RouterMandatory = 33,
        TcpKeepalive = 34,
        TcpKeepaliveCnt = 35,
        TcpKeepaliveIdle = 36,
        TcpKeepaliveIntvl = 37,
        TcpAcceptFilter = 38,
        DelayAttachOnConnect = 39,
        XpubVerbose = 40,
        RouterRawSocket = 41,

        Endian = 1000,

        [Obsolete]
        FailUnroutable = RouterMandatory,

        [Obsolete]
        RouterBehavior = RouterMandatory,
    }
}