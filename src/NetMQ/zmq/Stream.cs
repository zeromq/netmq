/*
    Copyright (c) 2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

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
using System.Linq;
using System.Text;

namespace NetMQ.zmq
{
    public class Stream : SocketBase
    {
        public class StreamSession : SessionBase
        {
            public StreamSession(IOThread ioThread, bool connect,
                                 SocketBase socket, Options options,
                                 Address addr)
                : base(ioThread, connect, socket, options, addr)
            {

            }
        }

        //  Fair queueing object for inbound pipes.
        private readonly FQ m_fq;

        //  True iff there is a message held in the pre-fetch buffer.
        private bool m_prefetched;

        //  If true, the receiver got the message part with
        //  the peer's identity.
        private bool m_identitySent;

        //  Holds the prefetched identity.
        private Msg m_prefetchedId;

        //  Holds the prefetched message.
        private Msg m_prefetchedMsg;

        class Outpipe
        {
            public Outpipe(Pipe pipe, bool active)
            {
                Pipe = pipe;
                Active = active;
            }

            public Pipe Pipe { get; private set; }
            public bool Active;
        };

        //  Outbound pipes indexed by the peer IDs.
        private readonly Dictionary<Blob, Outpipe> m_outpipes;

        //  The pipe we are currently writing to.
        private Pipe m_currentOut;

        //  If true, more outgoing message parts are expected.
        private bool m_moreOut;

        //  Peer ID are generated. It's a simple increment and wrap-over
        //  algorithm. This value is the next ID to use (if not used already).
        private int m_nextPeerId;

        public Stream(Ctx parent, int threadId, int sid)
            : base(parent, threadId, sid)
        {
            m_prefetched = false;
            m_identitySent = false;
            m_currentOut = null;
            m_moreOut = false;
            m_nextPeerId = Utils.GenerateRandom();

            m_options.SocketType = ZmqSocketType.Stream;


            m_fq = new FQ();
            m_prefetchedId = new Msg();
            m_prefetchedId.Init();
            m_prefetchedMsg = new Msg();
            m_prefetchedMsg.Init();

            m_outpipes = new Dictionary<Blob, Outpipe>();

            m_options.RawSocket = true;
        }

        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);

            IdentifyPeer(pipe);
            m_fq.Attach(pipe);
        }

        protected override void XTerminated(Pipe pipe)
        {
            Outpipe old;

            m_outpipes.TryGetValue(pipe.Identity, out  old);
            m_outpipes.Remove(pipe.Identity);

            Debug.Assert(old != null);

            m_fq.Terminated(pipe);
            if (pipe == m_currentOut)
                m_currentOut = null;
        }

        protected override void XReadActivated(Pipe pipe)
        {
            m_fq.Activated(pipe);
        }

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


        protected override bool XSend(ref Msg msg, SendReceiveOptions flags)
        {
            //  If this is the first part of the message it's the ID of the
            //  peer to send the message to.
            if (!m_moreOut)
            {
                Debug.Assert(m_currentOut == null);

                //  If we have malformed message (prefix with no subsequent message)
                //  then just silently ignore it.
                //  TODO: The connections should be killed instead.
                if (msg.HasMore)
                {
                    //  Find the pipe associated with the identity stored in the prefix.
                    //  If there's no such pipe just silently ignore the message, unless
                    //  mandatory is set.
                    Blob identity = new Blob(msg.Data, msg.Size);
                    Outpipe op;

                    if (m_outpipes.TryGetValue(identity, out op))
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
                        throw NetMQException.Create(ErrorCode.EHOSTUNREACH);
                    }
                }

                m_moreOut = true;

                return true;
            }

            //  Ignore the MORE flag
            msg.ResetFlags(MsgFlags.More);

            //  This is the last part of the message. 
            m_moreOut = false;

            //  Push the message into the pipe. If there's no out pipe, just drop it.
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
            else
            {
            }

            //  Detach the message from the data buffer.

            return true;
        }

        protected override bool XRecv(SendReceiveOptions flags, ref Msg msg)
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

            Pipe[] pipe = new Pipe[1];

            bool isMessageAvailable = m_fq.RecvPipe(pipe, ref m_prefetchedMsg);

            if (!isMessageAvailable)
            {
                return false;
            }            

            Debug.Assert(pipe[0] != null);
            Debug.Assert(!m_prefetchedMsg.HasMore);

            //  We have received a frame with TCP data.
            //  Rather than sendig this frame, we keep it in prefetched
            //  buffer and send a frame with peer's ID.
            Blob identity = pipe[0].Identity;            
            msg.InitSize(identity.Size);
            msg.Put(identity.Data, 0, identity.Size);
            msg.SetFlags(MsgFlags.More);

            m_prefetched = true;
            m_identitySent = true;

            return true;
        }

        protected override bool XHasIn()
        {
            //  We may already have a message pre-fetched.
            if (m_prefetched)
                return true;

            //  Try to read the next message.
            //  The message, if read, is kept in the pre-fetch buffer.
            Pipe[] pipe = new Pipe[1];

            bool isMessageAvailable = m_fq.RecvPipe(pipe, ref m_prefetchedMsg);

            if (!isMessageAvailable)
            {
                return false;
            }            

            Debug.Assert(pipe[0] != null);
            Debug.Assert(!m_prefetchedMsg.HasMore);

            Blob identity = pipe[0].Identity;
            m_prefetchedId = new Msg();
            m_prefetchedId.InitSize(identity.Size);
            m_prefetchedId.Put(identity.Data, 0, identity.Size);
            m_prefetchedId.SetFlags(MsgFlags.More);

            m_prefetched = true;
            m_identitySent = false;

            return true;
        }


        protected override bool XHasOut()
        {
            //  In theory, STREAM socket is always ready for writing. Whether actual
            //  attempt to write succeeds depends on whitch pipe the message is going
            //  to be routed to.
            return true;
        }

        private void IdentifyPeer(Pipe pipe)
        {
            Blob identity;

            // Always assign identity for raw-socket
            byte[] buf = new byte[5];
            buf[0] = 0;

            byte[] result = BitConverter.GetBytes(m_nextPeerId++);

            Buffer.BlockCopy(result, 0, buf, 1, 4);
            identity = new Blob(buf, buf.Length);
            m_options.Identity = buf;
            m_options.IdentitySize = (byte)buf.Length;

            pipe.Identity = identity;
            //  Add the record into output pipes lookup table
            Outpipe outpipe = new Outpipe(pipe, true);
            m_outpipes.Add(identity, outpipe);
        }
    }
}
