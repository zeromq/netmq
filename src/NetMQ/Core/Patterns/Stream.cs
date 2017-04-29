/*
    Copyright (c) 2012 iMatix Corporation
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using NetMQ.Core.Patterns.Utils;
using NetMQ.Core.Utils;

namespace NetMQ.Core.Patterns
{
    internal sealed class Stream : SocketBase
    {
        private static readonly Random s_random = new Random();

        public class StreamSession : SessionBase
        {
            public StreamSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        private class Outpipe
        {
            public Outpipe([NotNull] Pipe pipe, bool active)
            {
                Pipe = pipe;
                Active = active;
            }

            [NotNull]
            public Pipe Pipe { get; }

            public bool Active;
        }

        /// <summary>
        /// Fair queueing object for inbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        /// <summary>
        /// True if there is a message held in the pre-fetch buffer.
        /// </summary>
        private bool m_prefetched;

        /// <summary>
        /// If true, the receiver got the message part with
        /// the peer's identity.
        /// </summary>
        private bool m_identitySent;

        /// <summary>
        /// Holds the prefetched identity.
        /// </summary>
        private Msg m_prefetchedId;

        /// <summary>
        /// Holds the prefetched message.
        /// </summary>
        private Msg m_prefetchedMsg;

        /// <summary>
        /// Outbound pipes indexed by the peer IDs.
        /// </summary>
        private readonly Dictionary<byte[], Outpipe> m_outpipes;

        /// <summary>
        /// The pipe we are currently writing to.
        /// </summary>
        private Pipe m_currentOut;

        /// <summary>
        /// If true, more outgoing message parts are expected.
        /// </summary>
        private bool m_moreOut;

        /// <summary>
        /// Peer ID are generated. It's a simple increment and wrap-over
        /// algorithm. This value is the next ID to use (if not used already).
        /// </summary>
        private int m_nextPeerId;

        public Stream([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_prefetched = false;
            m_identitySent = false;
            m_currentOut = null;
            m_moreOut = false;
            m_nextPeerId = s_random.Next();

            m_options.SocketType = ZmqSocketType.Stream;

            m_fairQueueing = new FairQueueing();
            m_prefetchedId = new Msg();
            m_prefetchedId.InitEmpty();
            m_prefetchedMsg = new Msg();
            m_prefetchedMsg.InitEmpty();

            m_outpipes = new Dictionary<byte[], Outpipe>(new ByteArrayEqualityComparer());

            m_options.RawSocket = true;
        }

        public override void Destroy()
        {
            base.Destroy();

            m_prefetchedId.Close();
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

            IdentifyPeer(pipe);
            m_fairQueueing.Attach(pipe);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {

            m_outpipes.TryGetValue(pipe.Identity, out Outpipe old);
            m_outpipes.Remove(pipe.Identity);

            Debug.Assert(old != null);

            m_fairQueueing.Terminated(pipe);
            if (pipe == m_currentOut)
                m_currentOut = null;
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
            Outpipe outpipe = null;

            foreach (var it in m_outpipes)
            {
                if (it.Value.Pipe == pipe)
                {
                    Debug.Assert(!it.Value.Active);
                    it.Value.Active = true;
                    outpipe = it.Value;
                    break;
                }
            }

            Debug.Assert(outpipe != null);
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        /// <exception cref="HostUnreachableException">In Stream.XSend</exception>
        protected override bool XSend(ref Msg msg)
        {
            // If this is the first part of the message it's the ID of the
            // peer to send the message to.
            if (!m_moreOut)
            {
                Debug.Assert(m_currentOut == null);

                // If we have malformed message (prefix with no subsequent message)
                // then just silently ignore it.
                // TODO: The connections should be killed instead.
                if (msg.HasMore)
                {
                    // Find the pipe associated with the identity stored in the prefix.
                    // If there's no such pipe just silently ignore the message, unless
                    // mandatory is set.

                    var identity = msg.Size == msg.Data.Length
                        ? msg.Data
                        : msg.CloneData();

                    if (m_outpipes.TryGetValue(identity, out Outpipe op))
                    {
                        m_currentOut = op.Pipe;
                        if (!m_currentOut.CheckWrite())
                        {
                            op.Active = false;
                            m_currentOut = null;
                            return false;
                        }
                    }
                    else
                    {
                        throw new HostUnreachableException("In Stream.XSend");
                    }
                }

                m_moreOut = true;

                msg.Close();
                msg.InitEmpty();

                return true;
            }

            // Ignore the MORE flag
            msg.ResetFlags(MsgFlags.More);

            // This is the last part of the message.
            m_moreOut = false;

            // Push the message into the pipe. If there's no out pipe, just drop it.
            if (m_currentOut != null)
            {
                if (msg.Size == 0)
                {
                    m_currentOut.Terminate(false);
                    m_currentOut = null;
                    return true;
                }

                bool ok = m_currentOut.Write(ref msg);
                if (ok)
                {
                    m_currentOut.Flush();
                }

                m_currentOut = null;
            }

            // Detach the message from the data buffer.
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
            if (m_prefetched)
            {
                if (!m_identitySent)
                {
                    msg.Move(ref m_prefetchedId);
                    m_identitySent = true;
                }
                else
                {
                    msg.Move(ref m_prefetchedMsg);
                    m_prefetched = false;
                }

                return true;
            }

            var pipe = new Pipe[1];

            bool isMessageAvailable = m_fairQueueing.RecvPipe(pipe, ref m_prefetchedMsg);

            if (!isMessageAvailable)
            {
                return false;
            }

            Debug.Assert(pipe[0] != null);
            Debug.Assert(!m_prefetchedMsg.HasMore);

            // We have received a frame with TCP data.
            // Rather than sending this frame, we keep it in prefetched
            // buffer and send a frame with peer's ID.
            byte[] identity = pipe[0].Identity;
            msg.InitPool(identity.Length);
            msg.Put(identity, 0, identity.Length);
            msg.SetFlags(MsgFlags.More);

            m_prefetched = true;
            m_identitySent = true;

            return true;
        }

        protected override bool XHasIn()
        {
            // We may already have a message pre-fetched.
            if (m_prefetched)
                return true;

            // Try to read the next message.
            // The message, if read, is kept in the pre-fetch buffer.
            var pipe = new Pipe[1];

            bool isMessageAvailable = m_fairQueueing.RecvPipe(pipe, ref m_prefetchedMsg);

            if (!isMessageAvailable)
            {
                return false;
            }

            Debug.Assert(pipe[0] != null);
            Debug.Assert(!m_prefetchedMsg.HasMore);

            byte[] identity = pipe[0].Identity;
            m_prefetchedId = new Msg();
            m_prefetchedId.InitPool(identity.Length);
            m_prefetchedId.Put(identity, 0, identity.Length);
            m_prefetchedId.SetFlags(MsgFlags.More);

            m_prefetched = true;
            m_identitySent = false;

            return true;
        }

        protected override bool XHasOut()
        {
            // In theory, STREAM socket is always ready for writing. Whether actual
            // attempt to write succeeds depends on which pipe the message is going
            // to be routed to.
            return true;
        }

        private void IdentifyPeer([NotNull] Pipe pipe)
        {
            // Always assign identity for raw-socket
            var identity = new byte[5];

            byte[] result = BitConverter.GetBytes(m_nextPeerId++);

            Buffer.BlockCopy(result, 0, identity, 1, 4);

            m_options.Identity = identity;
            m_options.IdentitySize = (byte)identity.Length;

            pipe.Identity = identity;

            // Add the record into output pipes lookup table
            var outpipe = new Outpipe(pipe, true);
            m_outpipes.Add(identity, outpipe);
        }
    }
}