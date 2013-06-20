/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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

namespace NetMQ.zmq
{
	public class Pull : SocketBase {

		public class PullSession : SessionBase {
			public PullSession(IOThread ioThread, bool connect,
			                   SocketBase socket, Options options,
			                   Address addr)
				: base(ioThread, connect, socket, options, addr)
			{
            
			}
		}
    
		//  Fair queueing object for inbound pipes.
		private readonly FQ m_fq;
    
		public Pull(Ctx parent, int threadId, int sid) : base(parent, threadId, sid){

			m_options.SocketType = ZmqSocketType.Pull;
        
			m_fq = new FQ();
		}

		override
			protected void XAttachPipe(Pipe pipe, bool icanhasall) {
			Debug.Assert(pipe!=null);
			m_fq.Attach (pipe);
			}

    
		override
			protected void XReadActivated (Pipe pipe)
		{       
			m_fq.Activated (pipe);
		}   

		override
			protected void XTerminated(Pipe pipe) {
			m_fq.Terminated (pipe);
			}

		override
			protected bool XRecv(SendReceiveOptions flags, out Msg msg)
		{
			return m_fq.Recv (out msg);
		}
    
		override
			protected bool XHasIn ()
		{
			return m_fq.HasIn ();
		}       


	}
}
