/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2010-2015 Other contributors as noted in the AUTHORS file

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
    internal class XSub : SocketBase
    {
        /// <summary>
        /// An XSubSession is a subclass of SessionBase that provides nothing more.
        /// </summary>
        public class XSubSession : SessionBase
        {
            public XSubSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        /// <summary>
        /// Fair queueing object for inbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        /// <summary>
        /// Object for distributing the subscriptions upstream.
        /// </summary>
        private readonly Distribution m_distribution;

        /// <summary>
        /// The repository of subscriptions.
        /// </summary>
        private readonly Trie m_subscriptions;

        /// <summary>
        /// If true, 'message' contains a matching message to return on the
        /// next recv call.
        /// </summary>
        private bool m_hasMessage;

        private Msg m_message;

        /// <summary>
        /// If true, part of a multipart message was already received, but
        /// there are following parts still waiting.
        /// </summary>
        private bool m_moreIn;

        private bool m_moreOut;

        private static readonly Trie.TrieDelegate s_sendSubscription;

        static XSub()
        {
            s_sendSubscription = (data, size, arg) =>
            {
                var pipe = (Pipe)arg;

                // Create the subscription message.
                var msg = new Msg();
                msg.InitPool(size + 1);
                msg.Put(1);
                msg.Put(data, 1, size);

                // Send it to the pipe.
                bool sent = pipe.Write(ref msg);
                // If we reached the SNDHWM, and thus cannot send the subscription, drop
                // the subscription message instead. This matches the behaviour of
                // zmq_setsockopt(ZMQ_SUBSCRIBE, ...), which also drops subscriptions
                // when the SNDHWM is reached.
                if (!sent)
                    msg.Close();
            };
        }

        public XSub([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_options.SocketType = ZmqSocketType.Xsub;
            m_hasMessage = false;
            m_moreIn = false;

            m_options.Linger = 0;
            m_fairQueueing = new FairQueueing();
            m_distribution = new Distribution();
            m_subscriptions = new Trie();

            m_message = new Msg();
            m_message.InitEmpty();
        }

        public override void Destroy()
        {
            base.Destroy();
            m_message.Close();
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
            m_distribution.Attach(pipe);

            // Send all the cached subscriptions to the new upstream peer.
            m_subscriptions.Apply(s_sendSubscription, pipe);
            pipe.Flush();
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
            m_distribution.Activated(pipe);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {
            m_fairQueueing.Terminated(pipe);
            m_distribution.Terminated(pipe);
        }

        protected override void XHiccuped(Pipe pipe)
        {
            // Send all the cached subscriptions to the hiccuped pipe.
            m_subscriptions.Apply(s_sendSubscription, pipe);
            pipe.Flush();
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        protected override bool XSend(ref Msg msg)
        {
            int size = msg.Size;
            bool msgMore = msg.HasMore;
            try
            {
                if (!m_moreOut && size > 0 && msg[0] == 1)
                {
                    // Process the subscription.
                    if (m_subscriptions.Add(msg.Data, msg.Offset + 1, size - 1))
                    {
                        m_distribution.SendToAll(ref msg);
                        return true;
                    }
                }
                else if (!m_moreOut && size > 0 && msg[0] == 0)
                {
                    if (m_subscriptions.Remove(msg.Data, msg.Offset + 1, size - 1))
                    {
                        m_distribution.SendToAll(ref msg);
                        return true;
                    }
                }
                else
                {
                    // upstream message unrelated to sub/unsub
                    m_distribution.SendToAll(ref msg);

                    return true;
                }
            }
            finally
            {
                // set it before returning and before SendToAll (which destroys a message):
                // the first part of every message
                // (including the very first) will have it as false
                // when true, we do not check for the first byte
                m_moreOut = msgMore;
            }

            msg.Close();
            msg.InitEmpty();

            return true;
        }

        protected override bool XHasOut()
        {
            // Subscription can be added/removed anytime.
            return true;
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected override bool XRecv(ref Msg msg)
        {
            // If there's already a message prepared by a previous call to zmq_poll,
            // return it straight ahead.

            if (m_hasMessage)
            {
                msg.Move(ref m_message);
                m_hasMessage = false;
                m_moreIn = msg.HasMore;
                return true;
            }

            // TODO: This can result in infinite loop in the case of continuous
            // stream of non-matching messages which breaks the non-blocking recv
            // semantics.
            while (true)
            {
                // Get a message using fair queueing algorithm.
                bool isMessageAvailable = m_fairQueueing.Recv(ref msg);

                // If there's no message available, return immediately.
                // The same when error occurs.
                if (!isMessageAvailable)
                {
                    return false;
                }

                // Check whether the message matches at least one subscription.
                // Non-initial parts of the message are passed
                if (m_moreIn || !m_options.Filter || Match(msg))
                {
                    m_moreIn = msg.HasMore;
                    return true;
                }

                // Message doesn't match. Pop any remaining parts of the message
                // from the pipe.
                while (msg.HasMore)
                {
                    isMessageAvailable = m_fairQueueing.Recv(ref msg);

                    Debug.Assert(isMessageAvailable);
                }
            }
        }

        protected override bool XHasIn()
        {
            // There are subsequent parts of the partly-read message available.
            if (m_moreIn)
                return true;

            // If there's already a message prepared by a previous call to zmq_poll,
            // return straight ahead.
            if (m_hasMessage)
                return true;

            // TODO: This can result in infinite loop in the case of continuous
            // stream of non-matching messages.
            while (true)
            {
                // Get a message using fair queueing algorithm.
                bool isMessageAvailable = m_fairQueueing.Recv(ref m_message);

                // If there's no message available, return immediately.
                // The same when error occurs.
                if (!isMessageAvailable)
                {
                    return false;
                }

                // Check whether the message matches at least one subscription.
                if (!m_options.Filter || Match(m_message))
                {
                    m_hasMessage = true;
                    return true;
                }

                // Message doesn't match. Pop any remaining parts of the message
                // from the pipe.
                while (m_message.HasMore)
                {
                    isMessageAvailable = m_fairQueueing.Recv(ref m_message);

                    Debug.Assert(isMessageAvailable);
                }
            }
        }

        private bool Match(Msg msg)
        {
            return m_subscriptions.Check(msg.Data, msg.Offset, msg.Size);
        }
    }
}