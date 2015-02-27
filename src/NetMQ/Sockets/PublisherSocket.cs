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
        internal PublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// Don't invoke this on a PublisherSockete - you'll just get a NotSupportedException.
        /// </summary>
        /// <param name="msg">the Msg object to put it in</param>
        /// <param name="options">a SendReceiveOptions that may be None, or any of the bits DontWait, SendMore</param>
        public override void Receive(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Publisher doesn't support receiving");
        }        
    }
}
