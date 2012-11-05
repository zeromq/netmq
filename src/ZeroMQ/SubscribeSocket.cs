using zmq;

namespace ZeroMQ
{
    using System;    

    internal class SubscribeSocket : ZmqSocket
    {
        internal SubscribeSocket(SocketBase socketProxy, SocketType socketType)
            : base(socketProxy, socketType)
        {
        }

        public override int Send(byte[] buffer, int size, SocketFlags flags)
        {
            throw new NotSupportedException();
        }
    }
}
