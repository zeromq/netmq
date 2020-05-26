using System.Diagnostics;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    internal sealed class Scatter : SocketBase
    {
        /// <summary>
        /// Load balancer managing the outbound pipes.
        /// </summary>
        private readonly LoadBalancer m_loadBalancer;

        public Scatter(Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId, true)
        {
            m_options.SocketType = ZmqSocketType.Scatter;

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
            
            // Don't delay pipe termination as there is no one
            // to receive the delimiter.
            pipe.SetNoDelay();
            
            m_loadBalancer.Attach(pipe);
        }

        /// <summary>
        /// Indicate the given pipe as being ready for writing to by this socket.
        /// This gets called by the WriteActivated method.
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
            m_loadBalancer.Terminated(pipe);
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        protected override bool XSend(ref Msg msg)
        {
            if (msg.HasMore) 
                throw new InvalidException("SCATTER sockets do not allow multipart data (ZMQ_SNDMORE)");
            
            return m_loadBalancer.Send(ref msg);
        }

        protected override bool XHasOut()
        {
            return m_loadBalancer.HasOut();
        }
    }
}
