/*
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
using System.Diagnostics;
using NetMQ.Utils;

namespace NetMQ.Core.Patterns
{
    class Dealer : SocketBase
    {

        public class DealerSession : SessionBase
        {
            public DealerSession(IOThread ioThread, bool connect,
                                                     SocketBase socket, Options options,
                                                     Address addr)
                : base(ioThread, connect, socket, options, addr)
            {

            }
        }

        //  Messages are fair-queued from inbound pipes. And load-balanced to
        //  the outbound pipes.
        private readonly FQ m_fq;
        private readonly LB m_lb;

        //  Have we prefetched a message.
        private bool m_prefetched;

        private Msg m_prefetchedMsg;

        //  Holds the prefetched message.
        public Dealer(Ctx parent, int threadId, int sid)
            : base(parent, threadId, sid)
        {

            m_prefetched = false;
            m_options.SocketType = ZmqSocketType.Dealer;

            m_fq = new FQ();
            m_lb = new LB();
            //  TODO: Uncomment the following line when DEALER will become true DEALER
            //  rather than generic dealer socket.
            //  If the socket is closing we can drop all the outbound requests. There'll
            //  be noone to receive the replies anyway.
            //  options.delay_on_close = false;

            m_options.RecvIdentity = true;

            m_prefetchedMsg = new Msg();
            m_prefetchedMsg.InitEmpty();
        }

        public override void Destroy()
        {
            base.Destroy();

            m_prefetchedMsg.Close();
        }

        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {

            Debug.Assert(pipe != null);
            m_fq.Attach(pipe);
            m_lb.Attach(pipe);
        }

        protected override bool XSend(ref Msg msg, SendReceiveOptions flags)
        {
            return m_lb.Send(ref msg, flags);
        }


        protected override bool XRecv(SendReceiveOptions flags, ref Msg msg)
        {
            return xxrecv(flags, ref msg);
        }

        private bool xxrecv(SendReceiveOptions flags, ref Msg msg)
        {            
            //  If there is a prefetched message, return it.
            if (m_prefetched)
            {
                msg.Move(ref m_prefetchedMsg);
                
                m_prefetched = false;
                
                return true;
            }

            //  DEALER socket doesn't use identities. We can safely drop it and 
            while (true)
            {
                bool isMessageAvailable = m_fq.Recv(ref msg);

                if (!isMessageAvailable)
                {
                    return false;
                }
             
                if ((msg.Flags & MsgFlags.Identity) == 0)
                    break;
            }

            return true;
        }

        protected override bool XHasIn()
        {
            //  We may already have a message pre-fetched.
            if (m_prefetched)
                return true;

            //  Try to read the next message to the pre-fetch buffer.

            bool isMessageAvailable = xxrecv(SendReceiveOptions.DontWait, ref m_prefetchedMsg);

            if (!isMessageAvailable)
            {
                return false;
            }
            else
            {
                m_prefetched = true;
                return true;
            }
        }

        protected override bool XHasOut()
        {
            return m_lb.HasOut();
        }

        protected override void XReadActivated(Pipe pipe)
        {
            m_fq.Activated(pipe);
        }

        protected override void XWriteActivated(Pipe pipe)
        {
            m_lb.Activated(pipe);
        }

        protected override void XTerminated(Pipe pipe)
        {
            m_fq.Terminated(pipe);
            m_lb.Terminated(pipe);
        }
    }
}
