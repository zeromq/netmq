using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PairSocket is a NetMQSocket, usually used to synchronize two threads - using only one socket on each side.
    /// </summary>
    public class PairSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new PairSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal PairSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
