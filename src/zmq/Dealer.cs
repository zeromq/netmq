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

public class Dealer : SocketBase {
    
    public class DealerSession : SessionBase {
        public DealerSession(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_,
            Address addr_) : base(io_thread_, connect_, socket_, options_, addr_) {
            
        }
    }
    
    //  Messages are fair-queued from inbound pipes. And load-balanced to
    //  the outbound pipes.
    private FQ fq;
    private LB lb;

    //  Have we prefetched a message.
    private bool prefetched;
    
    private Msg prefetched_msg;

    //  Holds the prefetched message.
    public Dealer(Ctx parent_, int tid_, int sid_) : base(parent_, tid_, sid_) {
                
        prefetched = false;
        options.type = ZMQ.ZMQ_DEALER;
        
        fq = new FQ();
        lb = new LB();
        //  TODO: Uncomment the following line when DEALER will become true DEALER
        //  rather than generic dealer socket.
        //  If the socket is closing we can drop all the outbound requests. There'll
        //  be noone to receive the replies anyway.
        //  options.delay_on_close = false;
            
        options.recv_identity = true;
    }
    
    protected override void xattach_pipe(Pipe pipe_, bool icanhasall_) {
        
        Debug.Assert(pipe_ != null);
        fq.attach (pipe_);
        lb.attach (pipe_);
    }
    
    protected override bool xsend (Msg msg_, int flags_)
    {
        return lb.send (msg_, flags_);
    }

    
    protected override Msg xrecv (int flags_)
    {
        return xxrecv(flags_);
    }
    
    private Msg xxrecv (int flags_)
    {
        Msg msg_ = null;
        //  If there is a prefetched message, return it.
        if (prefetched) {
            msg_ = prefetched_msg ;
            prefetched = false;
            prefetched_msg = null;
            return msg_;
        }

        //  DEALER socket doesn't use identities. We can safely drop it and 
        while (true) {
            msg_ = fq.recv ();
            if (msg_ == null)
                return msg_;
            if ((msg_.flags & Msg.identity) == 0)
                break;
        }
        return msg_;
    }
    
    protected override bool xhas_in ()
    {
        //  We may already have a message pre-fetched.
        if (prefetched)
            return true;

        //  Try to read the next message to the pre-fetch buffer.
        prefetched_msg = xxrecv (ZMQ.ZMQ_DONTWAIT);
        if (prefetched_msg == null && ZError.IsError(ZError.EAGAIN))
            return false;
        prefetched = true;
        return true;
    }
    
    protected override bool xhas_out ()
    {
        return lb.has_out ();
    }
    
    protected override void xread_activated (Pipe pipe_)
    {
        fq.activated (pipe_);
    }
   
    protected override void xwrite_activated (Pipe pipe_)
    {
        lb.activated (pipe_);
    }

    protected override void xterminated(Pipe pipe_)
    {
        fq.terminated (pipe_);
        lb.terminated (pipe_);
    }
}
