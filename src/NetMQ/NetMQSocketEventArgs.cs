using System;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// This subclass of EventArgs contains a NetMQSocket,
    /// and IsReadyToReceive and IsReadyToSend flags to indicate whether ready to receive or send.
    /// </summary>
    public class NetMQSocketEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQSocketEventArgs referencing the given socket.
        /// </summary>
        /// <param name="socket">the NetMQSocket that this is in reference to</param>
        public NetMQSocketEventArgs([NotNull] NetMQSocket socket)
        {
            Socket = socket;
        }

        /// <summary>
        /// Initialise the ReceiveReady and SendReady flags from the given PollEvents value.
        /// </summary>
        /// <param name="events">a PollEvents value that indicates whether the socket is ready to send or receive without blocking</param>
        internal void Init(PollEvents events)
        {
            IsReadyToReceive = events.HasIn();
            IsReadyToSend = events.HasOut();
        }

        /// <summary>
        /// Get the NetMQSocket that this references.
        /// </summary>
        [NotNull]
        public NetMQSocket Socket { get; }

        /// <summary>
        /// Get whether at least one message may be received by the socket without blocking.
        /// </summary>
        public bool IsReadyToReceive { get; private set; }

        /// <summary>
        /// Get whether at least one message may be sent by the socket without blocking.
        /// </summary>
        public bool IsReadyToSend { get; private set; }
    }
}
