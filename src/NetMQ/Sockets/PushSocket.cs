using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Part of the push pull pattern, will push messages to push sockets
    /// </summary>
    public class PushSocket : NetMQSocket
    {
        public PushSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        public override void Receive(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Push socket doesn't support receiving");
        }
    }
}
