using System;
using NetMQ.zmq;


namespace NetMQ.Sockets
{
    /// <summary>
    /// A PublisherSocket is a NetMQSocket intended to be used as the Pub in the PubSub pattern.
    /// The intended usage is for publishing messages to all subscribers which are subscribed to a given topic.
    /// </summary>
    public class PublisherSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new PublisherSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal PublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary><see cref="PublisherSocket"/> doesn't support sending, so this override throws <see cref="NotSupportedException"/>.</summary>
        /// <param name="msg">the Msg object to put it in</param>
        /// <param name="options">a SendReceiveOptions that may be None, or any of the bits DontWait, SendMore</param>
        /// <exception cref="NotSupportedException">Receive is not supported on a PublisherSocket.</exception>
        [Obsolete("Use Receive(ref Msg) or TryReceive(ref Msg,TimeSpan) instead.")]
        public override void Receive(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("PublisherSocket doesn't support receiving");
        }

        /// <summary><see cref="PublisherSocket"/> doesn't support sending, so this override throws <see cref="NotSupportedException"/>.</summary>
        /// <exception cref="NotSupportedException">Receive is not supported.</exception>
        public override bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            throw new NotSupportedException("PublisherSocket doesn't support receiving");
        }
    }
}
