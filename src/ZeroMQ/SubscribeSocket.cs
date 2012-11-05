using zmq;

namespace ZeroMQ
{
    using System;    

    internal class SubscribeSocket : ZmqSocket
    {
        internal SubscribeSocket(SocketBase socketProxy, ZmqSocketType socketType)
            : base(socketProxy, socketType)
        {
        }

        public override int Send(byte[] buffer, int size, ZmqSocketType flags)
        {
            throw new NotSupportedException();
        }
    }
}
