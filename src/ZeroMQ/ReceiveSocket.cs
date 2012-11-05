using zmq;

namespace ZeroMQ
{
    using System;    

    internal class ReceiveSocket : ZmqSocket
    {
        internal ReceiveSocket(SocketBase socketProxy, ZmqSocketType socketType)
            : base(socketProxy, socketType)
        {
        }

        public override int Send(byte[] buffer, int size, SendRecieveOptions flags)
        {
            throw new NotSupportedException();
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
