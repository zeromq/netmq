using zmq;

namespace ZeroMQ
{
    using System;    

    internal class DuplexSocket : ZmqSocket
    {
        internal DuplexSocket(SocketBase socketProxy, SocketType socketType)
            : base(socketProxy, socketType)
        {
        }

        public override void Subscribe(byte[] prefix)
        {
            throw new NotSupportedException();
        }

        public override void Unsubscribe(byte[] prefix)
        {
            throw new NotSupportedException();
        }
    }
}
