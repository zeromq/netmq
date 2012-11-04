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

using System;
using System.Diagnostics;

public class Rep : Router {

    public class RepSession : Router.RouterSession {
        public RepSession(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_,
            Address addr_) : base(io_thread_, connect_, socket_, options_, addr_)
         {
        }
    }
    //  If true, we are in process of sending the reply. If false we are
    //  in process of receiving a request.
    private bool sending_reply;

    //  If true, we are starting to receive a request. The beginning
    //  of the request is the backtrace stack.
    private bool request_begins;
    
    
    public Rep(Ctx parent_, int tid_, int sid_) : base(parent_, tid_, sid_)
    {
        
        sending_reply = false;
        request_begins = true;

				options.SocketType = ZmqSocketType.ZMQ_REP;
    }
    
    override
		protected bool xsend(Msg msg_, ZmqSendRecieveOptions flags_)
    {
        //  If we are in the middle of receiving a request, we cannot send reply.
        if (!sending_reply) {
            throw new InvalidOperationException ("Cannot send another reply");
        }

        bool more = msg_.has_more();

        //  Push message to the reply pipe.
        bool rc = base.xsend (msg_, flags_);
        if (!rc)
            return rc;

        //  If the reply is complete flip the FSM back to request receiving state.
        if (!more)
            sending_reply = false;

        return true;
    }
    
    override
		protected Msg xrecv(ZmqSendRecieveOptions flags_)
    {
        //  If we are in middle of sending a reply, we cannot receive next request.
        if (sending_reply) {
            throw new InvalidOperationException("Cannot receive another request");
        }

        //  First thing to do when receiving a request is to copy all the labels
        //  to the reply pipe.
        if (request_begins) {
            while (true) {
                Msg msg_ = base.xrecv (flags_);
                if (msg_ == null)
                    return null;
                
                if (msg_.has_more()) {
                    //  Empty message part delimits the traceback stack.
                    bool bottom = (msg_.size == 0);
                    
                    //  Push it to the reply pipe.
                    bool rc = base.xsend (msg_, flags_);
                    Debug.Assert(rc);
                    if (bottom)
                        break;
                } else {
                    //  If the traceback stack is malformed, discard anything
                    //  already sent to pipe (we're at end of invalid message).
                    base.rollback();
                }
            }
            request_begins = false;
        }

        //  Get next message part to return to the user.
        Msg msg__ = base.xrecv (flags_);
        if (msg__ == null)
           return null;

        //  If whole request is read, flip the FSM to reply-sending state.
        if (!msg__.has_more()) {
            sending_reply = true;
            request_begins = true;
        }

        return msg__;
    }

    override
    protected bool xhas_in ()
    {
        if (sending_reply)
            return false;

        return base.xhas_in ();
    }
    
    override
    protected bool xhas_out ()
    {
        if (!sending_reply)
            return false;

        return base.xhas_out ();
    }



}
