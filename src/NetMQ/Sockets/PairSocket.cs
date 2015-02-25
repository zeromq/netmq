using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PairSocket is a NetMQSocket, usually used to synchronize two threads - using only one socket on each side.
    /// </summary>
    public class PairSocket : NetMQSocket
    {
        internal PairSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
