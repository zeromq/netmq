/*      
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2010 iMatix Corporation
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

public class Push : SocketBase {

    public class PushSession : SessionBase {
        public PushSession(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_,
            Address addr_) : base(io_thread_, connect_, socket_, options_, addr_) {
        }
    }
    
    //  Load balancer managing the outbound pipes.
    private LB lb;
    
    public Push(Ctx parent_, int tid_, int sid_) : base(parent_, tid_, sid_) {

			options.SocketType = ZmqSocketType.ZMQ_PUSH;
        
        lb = new LB();
    }

    override
    protected void xattach_pipe(Pipe pipe_, bool icanhasall_) {
        Debug.Assert(pipe_ != null);
        lb.attach (pipe_);
    }
    
    override
    protected void xwrite_activated (Pipe pipe_)
    {
        lb.activated (pipe_);
    }


    override
    protected void xterminated(Pipe pipe_) {
        lb.terminated (pipe_);
    }

    override
		protected bool xsend(Msg msg_, ZmqSendRecieveOptions flags_)
    {
        return lb.send (msg_, flags_);
    }
    
    override
    protected bool xhas_out ()
    {
        return lb.has_out ();
    }

}
