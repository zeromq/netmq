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

        protected internal override void ReceiveInternal(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Push socket doesn't support receiving");
        }
    }
}
