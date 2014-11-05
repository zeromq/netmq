using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Pair socket, usually used to synchronize two threads, only one socket on each side
    /// </summary>
    public class PairSocket : NetMQSocket
    {
        internal PairSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
