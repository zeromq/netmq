using System;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// This subclass of EventArgs contains a NetMQSocket, and ReceiveReady and SendReady flags to indicate whether ready to receive or send.
    /// </summary>
    public class NetMQSocketEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQSocketEventArgs referencing the given socket.
        /// </summary>
        /// <param name="socket">the NetMQSocket that this is about</param>
        public NetMQSocketEventArgs([NotNull] NetMQSocket socket)
        {
            Socket = socket;
        }

        /// <summary>
        /// Initialize the ReceiveReady and SendReady flags from the given PollEvents value.
        /// </summary>
        /// <param name="events">a PollEvents value that indicates whether the socket is ready to send or receive without blocking</param>
        internal void Init(PollEvents events)
        {
            ReceiveReady = events.HasFlag(PollEvents.PollIn);
            SendReady = events.HasFlag(PollEvents.PollOut);
        }

        [NotNull]
        public NetMQSocket Socket { get; private set; }

        /// <summary>
        /// Get whether at least one message may be received by the socket without blocking.
        /// </summary>
        public bool ReceiveReady { get; private set; }

        /// <summary>
        /// Get whether at least one message may be sent by the socket without blocking.
        /// </summary>
        public bool SendReady { get; private set; }
    }
}
