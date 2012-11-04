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

//TODO: This class uses O(n) scheduling. Rewrite it to use O(1) algorithm.
public class Router : SocketBase {

    public class RouterSession : SessionBase {
        public RouterSession(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_,
            Address addr_): base(io_thread_, connect_, socket_, options_, addr_) {
            
        }
    }
    
    //  Fair queueing object for inbound pipes.
    private FQ fq;

    //  True iff there is a message held in the pre-fetch buffer.
    private bool prefetched;

    //  If true, the receiver got the message part with
    //  the peer's identity.
    private bool identity_sent;

    //  Holds the prefetched identity.
    private Msg prefetched_id;

    //  Holds the prefetched message.
    private Msg prefetched_msg;

    //  If true, more incoming message parts are expected.
    private bool more_in;

    class Outpipe
    {
        public Pipe pipe;
        public bool active;
        
        public Outpipe(Pipe pipe_, bool active_) {
            pipe = pipe_;
            active = active_;
        }
    };

    //  We keep a set of pipes that have not been identified yet.
    private HashSet <Pipe> anonymous_pipes;

    //  Outbound pipes indexed by the peer IDs.
    private Dictionary<Blob, Outpipe> outpipes;

    //  The pipe we are currently writing to.
    private Pipe current_out;

    //  If true, more outgoing message parts are expected.
    private bool more_out;

    //  Peer ID are generated. It's a simple increment and wrap-over
    //  algorithm. This value is the next ID to use (if not used already).
    private int next_peer_id;

    // If true, report EHOSTUNREACH to the caller instead of silently dropping 
    // the message targeting an unknown peer.
    private bool mandatory;

    
    public Router(Ctx parent_, int tid_, int sid_) : base(parent_, tid_, sid_) {

        prefetched = false;
        identity_sent = false;
        more_in = false;
        current_out = null;
        more_out = false;
        next_peer_id = Utils.generate_random (); 
        mandatory = false;

				options.SocketType = ZmqSocketType.ZMQ_ROUTER;
        
        
        fq = new FQ();
        prefetched_id = new Msg();
        prefetched_msg = new Msg();
        
        anonymous_pipes = new HashSet<Pipe>();
        outpipes = new Dictionary<Blob, Outpipe>();
        
        //  TODO: Uncomment the following line when ROUTER will become true ROUTER
        //  rather than generic router socket.
        //  If peer disconnect there's noone to send reply to anyway. We can drop
        //  all the outstanding requests from that peer.
        //  options.delay_on_disconnect = false;
            
        options.RecvIdentity = true;
            
    }
    
    protected override void xattach_pipe(Pipe pipe_, bool icanhasall_) {
        Debug.Assert(pipe_ != null);

        bool identity_ok = identify_peer (pipe_);
        if (identity_ok)
            fq.attach (pipe_);
        else
            anonymous_pipes.Add (pipe_);
    }


		protected override bool xsetsockopt(ZmqSocketOptions option_, Object optval_)
    {
			if (option_ != ZmqSocketOptions.ZMQ_ROUTER_MANDATORY)
			{
            ZError.errno = (ZError.EINVAL);
            return false;
        }
        mandatory = (int) optval_ == 1;
        return true;
    }



    protected override void xterminated(Pipe pipe_)
    {
        if (!anonymous_pipes.Remove(pipe_)) {

            Outpipe old;

            outpipes.TryGetValue(pipe_.get_identity(), out  old);
            outpipes.Remove(pipe_.get_identity());
            
            Debug.Assert(old != null);

            fq.terminated (pipe_);
            if (pipe_ == current_out)
                current_out = null;
        }
    }
    
    
    protected override void xread_activated (Pipe pipe_)
    {
        if (!anonymous_pipes.Contains(pipe_)) 
            fq.activated (pipe_);
        else {
            bool identity_ok = identify_peer (pipe_);
            if (identity_ok) {
                anonymous_pipes.Remove(pipe_);
                fq.attach (pipe_);
            }
        }
    }
    
    
    protected override void xwrite_activated (Pipe pipe_)
    {
        foreach (var it in outpipes) {
            if (it.Value.pipe == pipe_) {
                Debug.Assert(!it.Value.active);
                it.Value.active = true;
                break;
            }
        }
        Debug.Assert(false);
    }


		protected override bool xsend(Msg msg_, ZmqSendRecieveOptions flags_)
    {
        //  If this is the first part of the message it's the ID of the
        //  peer to send the message to.
        if (!more_out) {
            Debug.Assert(current_out == null);

            //  If we have malformed message (prefix with no subsequent message)
            //  then just silently ignore it.
            //  TODO: The connections should be killed instead.
            if (msg_.has_more()) {

                more_out = true;

                //  Find the pipe associated with the identity stored in the prefix.
                //  If there's no such pipe just silently ignore the message, unless
                //  mandatory is set.
                Blob identity = new Blob(msg_.get_data());
                Outpipe op = outpipes[identity];

                if (op != null) {
                    current_out = op.pipe;
                    if (!current_out.check_write ()) {
                        op.active = false;
                        current_out = null;
                    }
                } else if (mandatory) {
                    more_out = false;
                    ZError.errno = (ZError.EHOSTUNREACH);
                    return false;
                }
            }

            return true;
        }

        //  Check whether this is the last part of the message.
        more_out = msg_.has_more();

        //  Push the message into the pipe. If there's no out pipe, just drop it.
        if (current_out != null) {
            bool ok = current_out.write (msg_);
            if (!ok)
                current_out = null;
            else if (!more_out) {
                current_out.flush ();
                current_out = null;
            }
        }
        else {
        }

        //  Detach the message from the data buffer.

        return true;
    }



		protected override Msg xrecv(ZmqSendRecieveOptions flags_)
    {
        Msg msg_ = null;
        if (prefetched) {
            if (!identity_sent) {
                msg_ = prefetched_id;
                prefetched_id = null;
                identity_sent = true;
            }
            else {
                msg_ = prefetched_msg;
                prefetched_msg = null;
                prefetched = false;
            }
            more_in = msg_.has_more();
            return msg_;
        }

        Pipe[] pipe = new Pipe[1];
        msg_ = fq.recvpipe (pipe);
        
        //  It's possible that we receive peer's identity. That happens
        //  after reconnection. The current implementation assumes that
        //  the peer always uses the same identity.
        //  TODO: handle the situation when the peer changes its identity.
        while (msg_ != null && msg_.is_identity ())
            msg_ = fq.recvpipe (pipe);

        if (msg_ == null)
            return null;
        
        Debug.Assert(pipe[0] != null);

        //  If we are in the middle of reading a message, just return the next part.
        if (more_in)
            more_in = msg_.has_more();
        else {
            //  We are at the beginning of a message.
            //  Keep the message part we have in the prefetch buffer
            //  and return the ID of the peer instead.
            prefetched_msg = msg_;

            prefetched = true;

            Blob identity = pipe[0].get_identity ();
            msg_ = new Msg(identity.data());
						msg_.SetFlags(MsgFlags.More);
            identity_sent = true;
        }

        return msg_;
    }
    
    //  Rollback any message parts that were sent but not yet flushed.
    protected void rollback () {
        
        if (current_out != null) {
            current_out.rollback ();
            current_out = null;
            more_out = false;
        }
    }
    
    
    protected override  bool xhas_in ()
    {
        //  If we are in the middle of reading the messages, there are
        //  definitely more parts available.
        if (more_in)
            return true;

        //  We may already have a message pre-fetched.
        if (prefetched)
            return true;

        //  Try to read the next message.
        //  The message, if read, is kept in the pre-fetch buffer.
        Pipe[] pipe = new Pipe[1];
        prefetched_msg = fq.recvpipe (pipe);

        //  It's possible that we receive peer's identity. That happens
        //  after reconnection. The current implementation assumes that
        //  the peer always uses the same identity.
        //  TODO: handle the situation when the peer changes its identity.
        while (prefetched_msg != null && prefetched_msg.is_identity ())
            prefetched_msg = fq.recvpipe (pipe);

        if (prefetched_msg == null)
            return false;

        Debug.Assert(pipe[0] != null);
        
        Blob identity = pipe[0].get_identity ();
        prefetched_id = new Msg(identity.data());
				prefetched_id.SetFlags(MsgFlags.More);

        prefetched = true;
        identity_sent = false;

        return true;
    }

    
    protected override  bool xhas_out ()
    {
        //  In theory, ROUTER socket is always ready for writing. Whether actual
        //  attempt to write succeeds depends on whitch pipe the message is going
        //  to be routed to.
        return true;
    }

    private bool identify_peer (Pipe pipe_)
    {
        Blob identity;

        Msg msg = pipe_.read ();
        if (msg == null)
            return false;

        if (msg.size == 0) {
            //  Fall back on the auto-generation
            byte[] buf = new byte[5];

            buf[0] = 0;

            byte[] result = BitConverter.GetBytes(next_peer_id++);
            //if (BitConverter.IsLittleEndian)
            //{
            //    Array.Reverse(result);
            //}

            Buffer.BlockCopy(result, 0, buf, 1, 4);
            identity = new Blob(buf);
        }
        else {
            identity = new Blob(msg.get_data());

            //  Ignore peers with duplicate ID.
            if (outpipes.ContainsKey(identity))
                return false;
        }

        pipe_.set_identity (identity);
        //  Add the record into output pipes lookup table
        Outpipe outpipe = new Outpipe(pipe_, true);
        outpipes.Add (identity, outpipe);

        return true;
    }


}
