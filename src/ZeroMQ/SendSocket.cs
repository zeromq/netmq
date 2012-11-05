using zmq;

namespace ZeroMQ
{
    using System;    

    internal class SendSocket : ZmqSocket
    {
        internal SendSocket(SocketBase socketProxy, ZmqSocketType socketType)
            : base(socketProxy, socketType)
        {
        }

        public override int Receive(byte[] buffer, SendRecieveOptions flags)
        {
            throw new NotSupportedException();
        }

        public override byte[] Receive(byte[] frame, SendRecieveOptions flags, out int size)
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
