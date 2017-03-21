/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Diagnostics;
using JetBrains.Annotations;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    /// <summary>
    /// A Dealer socket is a SocketBase that is used as the parent-class of the Req socket.
    /// It provides for a pre-fetched Msg, and skips identity-messages.
    /// </summary>
    internal class Dealer : SocketBase
    {
        /// <summary>
        /// A DealerSession is a SessionBase subclass that is contained within the Dealer class.
        /// </summary>
        public class DealerSession : SessionBase
        {
            /// <summary>
            /// Create a new DealerSession (which is just a SessionBase).
            /// </summary>
            /// <param name="ioThread">the I/O-thread to associate this with</param>
            /// <param name="connect"></param>
            /// <param name="socket"></param>
            /// <param name="options"></param>
            /// <param name="addr"></param>
            public DealerSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        /// <summary>
        /// Messages are fair-queued from inbound pipes. And load-balanced to
        /// the outbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        private readonly LoadBalancer m_loadBalancer;

        /// <summary>
        /// Have we prefetched a message.
        /// </summary>
        private bool m_prefetched;

        /// <summary>
        /// The Msg that we have pre-fetched.
        /// </summary>
        private Msg m_prefetchedMsg;

        /// <summary>
        /// Create a new Dealer socket that holds the prefetched message.
        /// </summary>
        public Dealer([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_prefetched = false;
            m_options.SocketType = ZmqSocketType.Dealer;

            m_fairQueueing = new FairQueueing();
            m_loadBalancer = new LoadBalancer();

            m_options.RecvIdentity = true;

            m_prefetchedMsg = new Msg();
            m_prefetchedMsg.InitEmpty();
        }

        /// <summary>
        /// Destroy this Dealer-socket and close out any pre-fetched Msg.
        /// </summary>
        public override void Destroy()
        {
            base.Destroy();

            m_prefetchedMsg.Close();
        }

        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">not used</param>
        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);
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
            return m_loadBalancer.Send(ref msg);
        }

        /// <summary>
        /// For a Dealer socket: If there's a pre-fetched message, snatch that.
        /// Otherwise, dump any identity messages and get the first non-identity message,
        /// or return false if there are no messages available.
        /// </summary>
        /// <param name="msg">a Msg to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected override bool XRecv(ref Msg msg)
        {
            return ReceiveInternal(ref msg);
        }

        /// <summary>
        /// If there's a pre-fetched message, snatch that.
        /// Otherwise, dump any identity messages and get the first non-identity message,
        /// or return false if there are no messages available.
        /// </summary>
        /// <param name="msg">a Msg to receive the message into</param>
        /// <returns>false if there were no messages to receive</returns>
        private bool ReceiveInternal(ref Msg msg)
        {
            // If there is a prefetched message, return it.
            if (m_prefetched)
            {
                msg.Move(ref m_prefetchedMsg);

                m_prefetched = false;

                return true;
            }

            // DEALER socket doesn't use identities. We can safely drop it and
            while (true)
            {
                bool isMessageAvailable = m_fairQueueing.Recv(ref msg);

                if (!isMessageAvailable)
                {
                    return false;
                }

                // Stop when we get any message that is not an Identity.
                if (!msg.IsIdentity)
                    break;
            }

            return true;
        }

        /// <summary>
        /// If there is a message available and one has not been pre-fetched yet,
        /// preserve that message as our pre-fetched one.
        /// </summary>
        /// <returns></returns>
        protected override bool XHasIn()
        {
            // We may already have a message pre-fetched.
            if (m_prefetched)
                return true;

            // Try to read the next message to the pre-fetch buffer.
            bool isMessageAvailable = ReceiveInternal(ref m_prefetchedMsg);

            if (!isMessageAvailable)
                return false;

            m_prefetched = true;
            return true;
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