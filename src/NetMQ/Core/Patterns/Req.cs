/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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

namespace NetMQ.Core.Patterns
{
    /// <summary>
    /// A Req is a Dealer socket that serves as the Request in a Request/Response pattern.
    /// </summary>
    internal sealed class Req : Dealer
    {
        /// <summary>
        /// If true, request was already sent and reply wasn't received yet or
        /// was received partially.
        /// </summary>
        private bool m_receivingReply;

        /// <summary>
        /// If true, we are starting to send/receive a message. The first part
        /// of the message must be empty message part (backtrace stack bottom).
        /// </summary>
        private bool m_messageBegins;

        /// <summary>
        /// Create a new Req (Request) socket with the given parent Ctx, thread and socket id.
        /// </summary>
        /// <param name="parent">the Ctx to contain this socket</param>
        /// <param name="threadId">an integer thread-id for this socket to execute on</param>
        /// <param name="socketId">the socket-id for this socket</param>
        public Req([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_receivingReply = false;
            m_messageBegins = true;
            m_options.SocketType = ZmqSocketType.Req;
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        /// <exception cref="FiniteStateMachineException">Cannot XSend on a Req while awaiting reply.</exception>
        protected override bool XSend(ref Msg msg)
        {
            // If we've sent a request and we still haven't got the reply,
            // we can't send another request.
            if (m_receivingReply)
                throw new FiniteStateMachineException("Req.XSend - cannot send another request");

            bool isMessageSent;

            // First part of the request is the request identity.
            if (m_messageBegins)
            {
                var bottom = new Msg();
                bottom.InitEmpty();
                bottom.SetFlags(MsgFlags.More);
                isMessageSent = base.XSend(ref bottom);

                if (!isMessageSent)
                    return false;

                m_messageBegins = false;
            }

            bool more = msg.HasMore;

            isMessageSent = base.XSend(ref msg);

            if (!isMessageSent)
                return false;

            // If the request was fully sent, flip the FSM into reply-receiving state.
            if (!more)
            {
                m_receivingReply = true;
                m_messageBegins = true;
            }

            return true;
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        /// <exception cref="FiniteStateMachineException">Req.XRecv expecting send, not receive.</exception>
        protected override bool XRecv(ref Msg msg)
        {
            bool isMessageAvailable;

            // If request wasn't send, we can't wait for reply.
            if (!m_receivingReply)
                throw new FiniteStateMachineException("Req.XRecv - cannot receive another reply");

            // First part of the reply should be the original request ID.
            if (m_messageBegins)
            {
                isMessageAvailable = base.XRecv(ref msg);

                if (!isMessageAvailable)
                    return false;

                if (!msg.HasMore || msg.Size != 0)
                {
                    while (true)
                    {
                        isMessageAvailable = base.XRecv(ref msg);
                        Debug.Assert(isMessageAvailable);
                        if (!msg.HasMore)
                            break;
                    }

                    msg.Close();
                    msg.InitEmpty();
                    return false;
                }

                m_messageBegins = false;
            }

            isMessageAvailable = base.XRecv(ref msg);
            if (!isMessageAvailable)
                return false;

            // If the reply is fully received, flip the FSM into request-sending state.
            if (!msg.HasMore)
            {
                m_receivingReply = false;
                m_messageBegins = true;
            }

            return true;
        }

        protected override bool XHasIn()
        {
            if (!m_receivingReply)
                return false;

            return base.XHasIn();
        }

        protected override bool XHasOut()
        {
            if (m_receivingReply)
                return false;

            return base.XHasOut();
        }

        public class ReqSession : DealerSession
        {
            private enum State
            {
                Identity,
                Bottom,
                Body
            }

            private State m_state;

            public ReqSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {
                m_state = State.Identity;
            }

            /// <exception cref="FaultException">ReqSession must be in a valid state when PushMsg is called.</exception>
            public override bool PushMsg(ref Msg msg)
            {
                // TODO the flags checks here don't check specific bits -- should they use HasMore instead? does this work with shared Msg objects?

                switch (m_state)
                {
                    case State.Bottom:
                        if (msg.Flags == MsgFlags.More && msg.Size == 0)
                        {
                            m_state = State.Body;
                            return base.PushMsg(ref msg);
                        }
                        break;
                    case State.Body:
                        if (msg.Flags == MsgFlags.More)
                            return base.PushMsg(ref msg);
                        if (msg.Flags == MsgFlags.None)
                        {
                            m_state = State.Bottom;
                            return base.PushMsg(ref msg);
                        }
                        break;
                    case State.Identity:
                        if (msg.Flags == MsgFlags.None)
                        {
                            m_state = State.Bottom;
                            return base.PushMsg(ref msg);
                        }
                        break;
                }

                throw new FaultException("Req.PushMsg default failure.");
            }

            protected override void Reset()
            {
                base.Reset();
                m_state = State.Identity;
            }
        }
    }
}