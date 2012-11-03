/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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

public class Req : Dealer {

    
    //  If true, request was already sent and reply wasn't received yet or
    //  was raceived partially.
    private bool receiving_reply;

    //  If true, we are starting to send/recv a message. The first part
    //  of the message must be empty message part (backtrace stack bottom).
    private bool message_begins;


    public Req(Ctx parent_, int tid_, int sid_)
        : base(parent_, tid_, sid_)
    {
        
        
        receiving_reply = false;
        message_begins = true;
        options.type = ZMQ.ZMQ_REQ;
    }
    
    
    protected override bool xsend (Msg msg_, int flags_)
    {
        //  If we've sent a request and we still haven't got the reply,
        //  we can't send another request.
        if (receiving_reply) {
            throw new InvalidOperationException("Cannot send another request");
        }

        bool rc;

        //  First part of the request is the request identity.
        if (message_begins) {
            Msg bottom = new Msg();
            bottom.SetFlags (Msg.more);
            rc = base.xsend (bottom, 0);
            if (!rc)
                return false;
            message_begins = false;
        }

        bool more = msg_.has_more();

        rc = base.xsend (msg_, flags_);
        if (!rc)
            return rc;

        //  If the request was fully sent, flip the FSM into reply-receiving state.
        if (!more) {
            receiving_reply = true;
            message_begins = true;
        }

        return true;
    }

    override
    protected Msg xrecv (int flags_)
    {
        Msg msg_ = null;
        //  If request wasn't send, we can't wait for reply.
        if (!receiving_reply) {
            ZError.errno = (ZError.EFSM);
            throw new InvalidOperationException("Cannot wait before send");
        }

        //  First part of the reply should be the original request ID.
        if (message_begins) {
            msg_ = base.xrecv (flags_);
            if (msg_ == null)
                return null;

            // TODO: This should also close the connection with the peer!
            if ( !msg_.has_more() || msg_.size != 0) {
                while (true) {
                    msg_ = base.xrecv (flags_);
                    Debug.Assert(msg_ != null);
                    if (!msg_.has_more())
                        break;
                }
                ZError.errno = (ZError.EAGAIN);
                return null;
            }

            message_begins = false;
        }

        msg_ = base.xrecv (flags_);
        if (msg_ == null)
            return null;

        //  If the reply is fully received, flip the FSM into request-sending state.
        if (!msg_.has_more()) {
            receiving_reply = false;
            message_begins = true;
        }

        return msg_;
    }
    
    override
    protected bool xhas_in ()
    {
        //  TODO: Duplicates should be removed here.

        if (!receiving_reply)
            return false;

        return base.xhas_in ();
    }

    override
    protected bool xhas_out()
    {
        if (receiving_reply)
            return false;

        return base.xhas_out ();
    }

    
    public class ReqSession : Dealer.DealerSession {
        
        
        enum State {
            identity,
            bottom,
            body
        };

        State state;
        
        public ReqSession(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_,
            Address addr_) :base(io_thread_, connect_, socket_, options_, addr_)
        {            
            state = State.identity;
        }
        
        override
        public bool push_msg (Msg msg_)
        {
            switch (state) {
                case State.bottom:
                if (msg_.flags == Msg.more && msg_.size == 0) {
                    state = State.body;
                    return base.push_msg (msg_);
                }
                break;
                case State.body:
                if (msg_.flags == Msg.more)
                    return base.push_msg (msg_);
                if (msg_.flags == 0) {
                    state = State.bottom;
                    return base.push_msg (msg_);
                }
                break;
                case State.identity:
                if (msg_.flags == 0) {
                    state = State.bottom;
                    return base.push_msg (msg_);
                }
                break;
            }
            
            throw new InvalidOperationException(state.ToString());
        }
        
        protected override void reset ()
        {
            base.reset ();
            state = State.identity;
        }

    }

}
