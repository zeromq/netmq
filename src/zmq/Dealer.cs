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

using System.Diagnostics;

namespace zmq
{
	public class Dealer : SocketBase {
    
		public class DealerSession : SessionBase {
			public DealerSession(IOThread ioThread, bool connect,
			                     SocketBase socket, Options options,
			                     Address addr) : base(ioThread, connect, socket, options, addr) {
            
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
		public Dealer(Ctx parent, int tid, int sid) : base(parent, tid, sid) {
                
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
		}
    
		protected override void XAttachPipe(Pipe pipe, bool icanhasall) {
        
			Debug.Assert(pipe != null);
			m_fq.Attach (pipe);
			m_lb.Attach (pipe);
		}

		protected override bool XSend(Msg msg, SendRecieveOptions flags)
		{
			return m_lb.Send (msg, flags);
		}


		protected override Msg XRecv(SendRecieveOptions flags)
		{
			return xxrecv(flags);
		}

		private Msg xxrecv(SendRecieveOptions flags_)
		{
			Msg msg_ = null;
			//  If there is a prefetched message, return it.
			if (m_prefetched) {
				msg_ = m_prefetchedMsg ;
				m_prefetched = false;
				m_prefetchedMsg = null;
				return msg_;
			}

			//  DEALER socket doesn't use identities. We can safely drop it and 
			while (true) {
				msg_ = m_fq.Recv ();
				if (msg_ == null)
					return msg_;
				if ((msg_.Flags & MsgFlags.Identity) == 0)
					break;
			}
			return msg_;
		}
    
		protected override bool XHasIn ()
		{
			//  We may already have a message pre-fetched.
			if (m_prefetched)
				return true;

			//  Try to read the next message to the pre-fetch buffer.
			m_prefetchedMsg = xxrecv(SendRecieveOptions.DontWait);
			if (m_prefetchedMsg == null && ZError.IsError(ErrorNumber.EAGAIN))
				return false;
			m_prefetched = true;
			return true;
		}
    
		protected override bool XHasOut ()
		{
			return m_lb.HasOut ();
		}
    
		protected override void XReadActivated (Pipe pipe)
		{
			m_fq.Activated (pipe);
		}
   
		protected override void XWriteActivated (Pipe pipe)
		{
			m_lb.Activated (pipe);
		}

		protected override void XTerminated(Pipe pipe)
		{
			m_fq.Terminated (pipe);
			m_lb.Terminated (pipe);
		}
	}
}
