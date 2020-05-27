using System;
using System.Collections.Generic;
using System.Diagnostics;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    /// <summary>
    /// A Server is a subclass of SocketBase
    /// </summary>
    internal class Server : SocketBase
    {
        private static readonly Random s_random = new Random();

        /// <summary>
        /// An instance of class Outpipe contains a Pipe and a boolean property Active.
        /// </summary>
        private class Outpipe
        {
            public Outpipe(Pipe pipe, bool active)
            {
                Pipe = pipe;
                Active = active;
            }

            public Pipe Pipe { get; }

            public bool Active;
        }

        /// <summary>
        /// Fair queueing object for inbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        /// <summary>
        /// Outbound pipes indexed by the Routing IDs.
        /// </summary>
        private readonly Dictionary<uint, Outpipe> m_outpipes;

        /// <summary>
        /// Peer ID are generated. It's a simple increment and wrap-over
        /// algorithm. This value is the next ID to use (if not used already).
        /// </summary>
        private UInt32 m_nextRoutingId;

        /// <summary>
        /// Create a new Router instance with the given parent-Ctx, thread-id, and socket-id.
        /// </summary>
        /// <param name="parent">the Ctx that will contain this Router</param>
        /// <param name="threadId">the integer thread-id value</param>
        /// <param name="socketId">the integer socket-id value</param>
        public Server(Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId, true)
        {
            m_nextRoutingId = (uint) s_random.Next();
            m_options.SocketType = ZmqSocketType.Server;
            m_options.CanSendHelloMsg = true;
            m_fairQueueing = new FairQueueing();
            m_outpipes = new Dictionary<uint, Outpipe>();
        }

        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">not used</param>
        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            uint routingId = m_nextRoutingId++;
            if (routingId == 0)
                routingId = m_nextRoutingId++; //  Never use Routing ID zero

            pipe.RoutingId = routingId;
            //  Add the record into output pipes lookup table
            var outpipe = new Outpipe(pipe, true);
            m_outpipes.Add(routingId, outpipe);

            m_fairQueueing.Attach(pipe);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {
            bool removed = m_outpipes.Remove(pipe.RoutingId);
            Debug.Assert(removed);
            m_fairQueueing.Terminated(pipe);
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
        /// This gets called by the WriteActivated method.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for writing</param>
        protected override void XWriteActivated(Pipe pipe)
        {
            if (m_outpipes.TryGetValue(pipe.RoutingId, out var outpipe))
                outpipe.Active = true;
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        /// <exception cref="HostUnreachableException">The receiving host must be identifiable.</exception>
        protected override bool XSend(ref Msg msg)
        {
            //  SERVER sockets do not allow multipart data (ZMQ_SNDMORE)
            if (msg.HasMore)
                throw new InvalidException();

            //  Find the pipe associated with the routing stored in the message.
            uint routingId = msg.RoutingId;
            if (m_outpipes.TryGetValue(routingId, out var outpipe))
            {
                if (!outpipe.Pipe.CheckWrite())
                {
                    outpipe.Active = false;
                    return false;
                }
            }
            else
            {
                // TODO: support throwing HostUnreachableException with socket options
                // Dropping silently
                msg.Close();
                msg.InitEmpty();
                return true;
            }

            //  Message might be delivered over inproc, so we reset routing id
            msg.ResetRoutingId();

            bool ok = outpipe.Pipe.Write(ref msg);
            if (ok)
                outpipe.Pipe.Flush();
            else
                msg.Close();

            msg.InitEmpty();

            return true;
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected override bool XRecv(ref Msg msg)
        {
            bool received = m_fairQueueing.RecvPipe(ref msg, out var pipe);

            // Drop any messages with more flag
            while (received && msg.HasMore)
            {
                // drop all frames of the current multi-frame message
                received = m_fairQueueing.Recv(ref msg);

                while (received && msg.HasMore)
                    received = m_fairQueueing.Recv(ref msg);

                // get the new message
                if (received)
                    received = m_fairQueueing.RecvPipe(ref msg, out pipe);
            }

            if (!received)
                return false;

            Assumes.NotNull(pipe);
            msg.RoutingId = pipe.RoutingId;

            return true;
        }

        protected override bool XHasIn()
        {
            return m_fairQueueing.HasIn();
        }

        protected override bool XHasOut()
        {
            // In theory, SERVER socket is always ready for writing. Whether actual
            // attempt to write succeeds depends on which pipe the message is going
            // to be routed to.
            return true;
        }
    }
}