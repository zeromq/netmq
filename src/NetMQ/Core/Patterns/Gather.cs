using System.Diagnostics;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    internal sealed class Gather : SocketBase
    {
        /// <summary>
        /// Fair queueing object for inbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        public Gather(Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_options.SocketType = ZmqSocketType.Gather;

            m_fairQueueing = new FairQueueing();
        }

        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">not used</param>
        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Assumes.NotNull(pipe);
            m_fairQueueing.Attach(pipe);
        }

        /// <summary>
        /// Indicate the given pipe as being ready for reading by this socket.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for reading</param>
        protected override void XReadActivated(Pipe pipe)
        {
            m_fairQueueing.Activated(pipe);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {
            m_fairQueueing.Terminated(pipe);
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected override bool XRecv(ref Msg msg)
        {
            bool received = m_fairQueueing.Recv(ref msg);

            // Drop any messages with more flag
            while (received && msg.HasMore) 
            {
                // drop all frames of the current multi-frame message
                received = m_fairQueueing.Recv(ref msg);

                while (received && msg.HasMore)
                    received = m_fairQueueing.Recv(ref msg);

                // get the new message
                if (received)
                    received = m_fairQueueing.Recv(ref msg);
            }

            if (!received)
                return false;

            return true;
        }

        protected override bool XHasIn()
        {
            return m_fairQueueing.HasIn();
        }
    }
}
