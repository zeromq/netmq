using System.Diagnostics;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    /// <summary>
    /// A Client socket is a SocketBase.
    /// </summary>
    internal class Client : SocketBase
    {
        /// <summary>
        /// Messages are fair-queued from inbound pipes. And load-balanced to
        /// the outbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        private readonly LoadBalancer m_loadBalancer;

        /// <summary>
        /// Create a new Dealer socket that holds the prefetched message.
        /// </summary>
        public Client(Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId, true)
        {
            m_options.SocketType = ZmqSocketType.Client;
            m_options.CanSendHelloMsg = true;

            m_fairQueueing = new FairQueueing();
            m_loadBalancer = new LoadBalancer();
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
            m_loadBalancer.Attach(pipe);
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        protected override bool XSend(ref Msg msg)
        {
            //  CLIENT sockets do not allow multipart data (ZMQ_SNDMORE)
            if (msg.HasMore) 
                throw new InvalidException();
            
            return m_loadBalancer.Send(ref msg);
        }
        
        /// <summary>
        /// Get a message from FairQueuing data structure
        /// </summary>
        /// <param name="msg">a Msg to receive the message into</param>
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
        
        /// <summary>
        /// If there is a message available and one has not been pre-fetched yet,
        /// preserve that message as our pre-fetched one.
        /// </summary>
        /// <returns></returns>
        protected override bool XHasIn()
        {
            return m_fairQueueing.HasIn();
        }

        protected override bool XHasOut()
        {
            return m_loadBalancer.HasOut();
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
        /// Indicate the given pipe as being ready for writing to by this socket.
        /// This gets called by the WriteActivated method
        /// and gets overridden by the different sockets
        /// to provide their own concrete implementation.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for writing</param>
        protected override void XWriteActivated(Pipe pipe)
        {
            m_loadBalancer.Activated(pipe);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {
            m_fairQueueing.Terminated(pipe);
            m_loadBalancer.Terminated(pipe);
        }
    }
}