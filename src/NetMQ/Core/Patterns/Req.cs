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

using System;
using System.Diagnostics;

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
        /// if true, strict semantics are obeyed for request/reply patterns.  If false,
        /// relaxed semantics are used in accordance with ZMQ_REQ_RELAXED.
        /// </summary>
        private bool m_strict;

        /// <summary>
        /// if true, request id's are correlated in accordance with ZMQ_REQ_CORRELATE.
        /// </summary>
        private bool m_request_id_frames_enabled;

        /// <summary>
        /// A request identifier used in conjunction with m_request_id_frames_enabled; used
        /// to match (correlate) the reply to the corresponding request.
        /// </summary>
        private UInt32 m_request_id;

        /// <summary>
        /// specific pipe that we are expecting the reply to occur on, once we make a request.
        /// </summary>
        private Pipe? m_replyPipe;

        /// <summary>
        /// Create a new Req (Request) socket with the given parent Ctx, thread and socket id.
        /// </summary>
        /// <param name="parent">the Ctx to contain this socket</param>
        /// <param name="threadId">an integer thread-id for this socket to execute on</param>
        /// <param name="socketId">the socket-id for this socket</param>
        public Req(Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_receivingReply = false;
            m_messageBegins = true;
            m_options.SocketType = ZmqSocketType.Req;
            m_options.CanSendHelloMsg = false;
            m_strict = true;
            m_request_id_frames_enabled = false;
            m_request_id = (UInt32)(new Random()).Next(1, 10433); // just an arbitrary pick.  Don't want to make it likely that m_request_id overflows.
            m_replyPipe = null;
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
            if (m_receivingReply && m_strict)
                throw new FiniteStateMachineException("Req.XSend - cannot send another request");

            bool isMessageSent;

            // First part of the request is the request identity.
            if (m_messageBegins)
            {
                m_replyPipe = null;

                // support frame id
                if (m_request_id_frames_enabled)
                {
                    m_request_id++;
                    var requestid = new Msg();
                    requestid.InitEmpty();
                    requestid.InitPool(sizeof(UInt32));
                    requestid.Put(BitConverter.GetBytes(m_request_id), 0, sizeof(UInt32));
                    requestid.SetFlags(MsgFlags.More);
    
                    m_replyPipe = null;
                    if (!base.XSendPipe(ref requestid, out m_replyPipe))
                        return false;
                }

                var bottom = new Msg();
                bottom.InitEmpty();
                bottom.SetFlags(MsgFlags.More);
                isMessageSent = base.XSendPipe(ref bottom, out m_replyPipe);

                if (!isMessageSent)
                    return false;
                Debug.Assert(m_replyPipe != null);

                m_messageBegins = false;

                // Eat all currently available messages before the request is fully
                // sent. This is done to avoid:
                //   REQ sends request to A, A replies, B replies too.
                //   A's reply was first and matches, that is used.
                //   An hour later REQ sends a request to B. B's old reply is used.
                Msg drop = new Msg();
                while (true)
                {
                    drop.InitEmpty();
                    if (!base.XRecv(ref drop)) break;
                    drop.Close();
                }
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

            while (m_messageBegins)
            {
                // if enabled, the first frame must have the correct request_id.
                if (m_request_id_frames_enabled)
                {
                    if (!RecvFromReplyPipe(ref msg)) return false;

                    if (!msg.HasMore || (msg.Size != sizeof(UInt32)) || (BitConverter.ToUInt32(msg.Slice(0, sizeof(UInt32)).ToArray(), 0) != m_request_id))
                    {
                        // skip the remaining frames and try the next message
                        msg = SkipRemainingFrames(msg);
                        continue;
                    }
                }

                isMessageAvailable = RecvFromReplyPipe(ref msg);

                if (!isMessageAvailable)
                    return false;

                if (!msg.HasMore || msg.Size != 0)
                {
                    // this logic is distinctly different from the corresponding code in the C reference implementation (req.cpp).
                    // this should be checked on.
                    while (true)
                    {
                        isMessageAvailable = RecvFromReplyPipe(ref msg);
                        Debug.Assert(isMessageAvailable);
                        if (!msg.HasMore)
                            break;
                    }

                    msg.Close();
                    msg.InitEmpty();

                    continue;
                }

                m_messageBegins = false;
            }

            isMessageAvailable = RecvFromReplyPipe(ref msg);
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

        private Msg SkipRemainingFrames(Msg msg)
        {
            while (msg.HasMore)
            {
                var wasReceived = RecvFromReplyPipe(ref msg);
                Debug.Assert(wasReceived);
            }

            return msg;
        }

        bool RecvFromReplyPipe(ref Msg msg)
        {
            while (true)
            {
                if (!base.XRecvPipe(ref msg, out Pipe? pipe)) return false; 
                if ((m_replyPipe==null) || (pipe == m_replyPipe)) return true;
            }
        }

        protected override void XTerminated(Pipe pipe)
        {
            if (m_replyPipe == pipe) m_replyPipe = null;
            base.XTerminated(pipe);
        }

        protected override bool XHasIn()
        {
            if (!m_receivingReply)
                return false;

            return base.XHasIn();
        }

        protected override bool XHasOut()
        {
            if (m_receivingReply && m_strict)
                return false;

            return base.XHasOut();
        }

        protected override bool XSetSocketOption(ZmqSocketOption option, object? optionValue)
        {
            // bail if optionValue is a null or not a bool.
            if (optionValue == null || ((optionValue as bool?) == null))
            {
                return false;
            }

            var value = (bool)optionValue;
          
            switch (option)
            {
                case ZmqSocketOption.Correlate:
                    m_request_id_frames_enabled = value;
                    return true; 
          
                case ZmqSocketOption.Relaxed:
                    m_strict = !value;
                    return true; 
            }
  
            return base.XSetSocketOption(option, optionValue);
        }

        public class ReqSession : SessionBase
        {
            private enum State
            {
                Bottom,
                Body,
                RequestId
            }

            private State m_state;

            public ReqSession(IOThread ioThread, bool connect, SocketBase socket, Options options, Address addr)
                : base(ioThread, connect, socket, options, addr)
            {
                m_state = State.Bottom;
            }
            
            public override PushMsgResult PushMsg(ref Msg msg)
            {
                // TODO the flags checks here don't check specific bits -- should they use HasMore instead? does this work with shared Msg objects?

                switch (m_state)
                {
                    case State.Bottom:
                        if (msg.HasMore)
                        {
                            //  In case option ZMQ_CORRELATE is on, allow RequestId to be
                            //  transfered as first frame (would be too cumbersome to check
                            //  whether the option is actually on or not).
                            if (msg.Size == sizeof(UInt32))
                            {
                                m_state = State.RequestId;
                                return base.PushMsg(ref msg);
                            }
                            if (msg.Size == 0)
                            {
                                m_state = State.Body;
                                return base.PushMsg(ref msg);
                            }
                        }
                        break;

                    case State.RequestId:
                        if (msg.HasMore && msg.Size == 0)
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
                }

                return PushMsgResult.Error;
            }

            protected override void Reset()
            {
                base.Reset();
                m_state = State.Bottom;
            }
        }
    }
}