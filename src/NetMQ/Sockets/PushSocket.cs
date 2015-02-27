using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PushSocket is a NetMQSocket intended to be used as the "Push" part of the Push-Pull pattern.
    /// This will "push" messages to be pulled by the "pull" socket.
    /// </summary>
    public class PushSocket : NetMQSocket
    {
        internal PushSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// Don't invoke this on a PushSocket - you'll just get a NotSupportedException.
        /// </summary>
        /// <param name="msg">the Msg object to put it in</param>
        /// <param name="options">a SendReceiveOptions that may be None, or any of the bits DontWait, SendMore</param>
        public override void Receive(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Push socket doesn't support receiving");
        }
    }
}
