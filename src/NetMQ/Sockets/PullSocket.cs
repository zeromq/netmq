using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PullSocket is a NetMQSocket intended to be used as the "Pull" part of the Push-Pull pattern.
    /// This will "pull" messages that have been pushed from the "push" socket.
    /// </summary>
    public class PullSocket : NetMQSocket
    {
        internal PullSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// Don't invoke this on a PullSocket - you'll just get a NotSupportedException.
        /// </summary>
        /// <param name="msg">the Msg to transmit</param>
        /// <param name="options">a SendReceiveOptions that may be None, or any of the bits DontWait, SendMore</param>
        public override void Send(ref Msg msg, SendReceiveOptions options)
        {        
            throw new NotSupportedException("Pull socket doesn't support sending");
        }
    }
}
