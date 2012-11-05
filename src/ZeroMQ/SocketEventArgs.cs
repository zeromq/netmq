namespace ZeroMQ
{
    using System;
    using zmq;    

    /// <summary>
    /// Provides data for <see cref="ZmqSocket.ReceiveReady"/> and <see cref="ZmqSocket.SendReady"/> events.
    /// </summary>
    public class SocketEventArgs : EventArgs
    {
        internal SocketEventArgs(ZmqSocket socket, PollEvents readyEvents)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            this.Socket = socket;
            this.ReceiveReady = readyEvents.HasFlag(PollEvents.PollIn);
            this.SendReady = readyEvents.HasFlag(PollEvents.PollOut);
        }

        /// <summary>
        /// Gets the socket that may be used to receive or send at least one message without blocking.
        /// </summary>
        public ZmqSocket Socket { get; private set; }

        /// <summary>
        /// Gets a value indicating whether at least one message may be received by the socket without blocking.
        /// </summary>
        public bool ReceiveReady { get; private set; }

        /// <summary>
        /// Gets a value indicating whether at least one message may be sent by the socket without blocking.
        /// </summary>
        public bool SendReady { get; private set; }
    }
}
