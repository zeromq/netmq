/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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

using JetBrains.Annotations;

namespace NetMQ.Core.Patterns
{
    internal sealed class Rep : Router
    {
        public class RepSession : RouterSession
        {
            public RepSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        /// <summary>
        /// If true, we are in process of sending the reply. If false we are
        /// in process of receiving a request.
        /// </summary>
        private bool m_sendingReply;

        /// <summary>
        /// If true, we are starting to receive a request. The beginning
        /// of the request is the backtrace stack.
        /// </summary>
        private bool m_requestBegins;

        public Rep([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_sendingReply = false;
            m_requestBegins = true;

            m_options.SocketType = ZmqSocketType.Rep;
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        /// <exception cref="FiniteStateMachineException">XSend must only be called on Rep when in the state of sending a reply.</exception>
        protected override bool XSend(ref Msg msg)
        {
            // If we are in the middle of receiving a request, we cannot send reply.
            if (!m_sendingReply)
            {
                throw new FiniteStateMachineException("Rep.XSend - cannot send another reply");
            }

            bool more = msg.HasMore;

            // Push message to the reply pipe.
            bool isMessageSent = base.XSend(ref msg);

            if (!isMessageSent)
                return false;

            // If the reply is complete flip the FSM back to request receiving state.
            if (!more)
                m_sendingReply = false;

            return true;
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        /// <exception cref="FiniteStateMachineException">XRecv must not be called on Rep while in the state of sending a reply.</exception>
        protected override bool XRecv(ref Msg msg)
        {
            bool isMessageAvailable;

            // If we are in middle of sending a reply, we cannot receive next request.
            if (m_sendingReply)
                throw new FiniteStateMachineException("Rep.XRecv - cannot receive another request");

            // First thing to do when receiving a request is to copy all the labels
            // to the reply pipe.
            if (m_requestBegins)
            {
                while (true)
                {
                    isMessageAvailable = base.XRecv(ref msg);

                    if (!isMessageAvailable)
                        return false;

                    if (msg.HasMore)
                    {
                        // Empty message part delimits the traceback stack.
                        bool bottom = (msg.Size == 0);

                        // Push it to the reply pipe.
                        isMessageAvailable = base.XSend(ref msg);

                        if (!isMessageAvailable)
                            return false;

                        if (bottom)
                            break;
                    }
                    else
                    {
                        // If the traceback stack is malformed, discard anything
                        // already sent to pipe (we're at end of invalid message).
                        base.Rollback();
                    }
                }
                m_requestBegins = false;
            }

            // Get next message part to return to the user.
            isMessageAvailable = base.XRecv(ref msg);

            if (!isMessageAvailable)
            {
                return false;
            }

            // If whole request is read, flip the FSM to reply-sending state.
            if (!msg.HasMore)
            {
                m_sendingReply = true;
                m_requestBegins = true;
            }

            return true;
        }

        protected override bool XHasIn()
        {
            if (m_sendingReply)
                return false;

            return base.XHasIn();
        }

        protected override bool XHasOut()
        {
            if (!m_sendingReply)
                return false;

            return base.XHasOut();
        }
    }
}