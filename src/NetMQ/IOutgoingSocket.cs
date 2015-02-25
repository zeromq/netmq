using NetMQ.zmq;

namespace NetMQ
{
    public interface IOutgoingSocket
    {        
        /// <summary>
        /// Send the given byte-array of data out upon this socket.
        /// </summary>
        void Send(ref Msg msg, SendReceiveOptions options);
    }
}
