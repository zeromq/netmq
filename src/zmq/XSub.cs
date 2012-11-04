/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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

public class XSub : SocketBase {
    
    public  class XSubSession : SessionBase {

        public XSubSession(IOThread io_thread_, bool connect_,
                SocketBase socket_, Options options_, Address addr_) : 
            base(io_thread_, connect_, socket_, options_, addr_) {
            }
        
    }
    
    //  Fair queueing object for inbound pipes.
    private FQ fq;

    //  Object for distributing the subscriptions upstream.
    private Dist dist;

    //  The repository of subscriptions.
    private Trie subscriptions;

    //  If true, 'message' contains a matching message to return on the
    //  next recv call.
    private bool has_message;
    private Msg message;

    //  If true, part of a multipart message was already received, but
    //  there are following parts still waiting.
    private bool more;
    private static Trie.ITrieDelegate send_subscription;

    static XSub ()
	{
        send_subscription = (data_, size, arg_) => {
            
 
                
                Pipe pipe = (Pipe) arg_;

                //  Create the subsctription message.
                Msg msg = new Msg(size + 1);
                msg.put((byte)1);
                msg.put(data_,1, size);

                //  Send it to the pipe.
                bool sent = pipe.write (msg);
                //  If we reached the SNDHWM, and thus cannot send the subscription, drop
                //  the subscription message instead. This matches the behaviour of
                //  zmq_setsockopt(ZMQ_SUBSCRIBE, ...), which also drops subscriptions
                //  when the SNDHWM is reached.
                if (!sent)
                    msg.close ();

            
        };
    }
    
    public XSub (Ctx parent_, int tid_, int sid_) : base(parent_, tid_, sid_) {

			options.SocketType = ZmqSocketType.ZMQ_XSUB;
        has_message = false;
        more = false;
        
        options.Linger = 0;
        fq = new FQ();
        dist = new Dist();
        subscriptions = new Trie();
        
        
    }
        
    protected override void xattach_pipe (Pipe pipe_, bool icanhasall_)
    {
        Debug.Assert(pipe_ != null);
        fq.attach (pipe_);
        dist.attach (pipe_);

        //  Send all the cached subscriptions to the new upstream peer.
        subscriptions.apply (send_subscription, pipe_);
        pipe_.flush ();
    }
    
    

    protected override void xread_activated (Pipe pipe_) {
        fq.activated (pipe_);
    }
    
    protected override void xwrite_activated (Pipe pipe_)
    {
        dist.activated (pipe_);
    }
    
    protected override void xterminated (Pipe pipe_)
    {
        fq.terminated (pipe_);
        dist.terminated (pipe_);
    }
    
    protected override void xhiccuped (Pipe pipe_)
    {
        //  Send all the cached subscriptions to the hiccuped pipe.
        subscriptions.apply (send_subscription, pipe_);
        pipe_.flush ();
    }

		protected override bool xsend(Msg msg_, ZmqSendRecieveOptions flags_)
    {
        byte[] data = msg_.get_data(); 
        // Malformed subscriptions.
        if (data.Length < 1 || (data[0] != 0 && data[0] != 1)) {
            return false;
        }
        
        // Process the subscription.
        if (data[0] == 1) {
            if (subscriptions.add (data , 1))
                return dist.send_to_all (msg_, flags_);
        }
        else {
            if (subscriptions.rm (data, 1))
                return dist.send_to_all (msg_, flags_);
        }

        return true;
    }

    protected override bool xhas_out () {
        //  Subscription can be added/removed anytime.
        return true;
    }

		protected override Msg xrecv(ZmqSendRecieveOptions flags_)
		{
        //  If there's already a message prepared by a previous call to zmq_poll,
        //  return it straight ahead.
        Msg msg_;
        if (has_message) {
            msg_ = new Msg(message);
            has_message = false;
            more = msg_.has_more();
            return msg_;
        }

        //  TODO: This can result in infinite loop in the case of continuous
        //  stream of non-matching messages which breaks the non-blocking recv
        //  semantics.
        while (true) {

            //  Get a message using fair queueing algorithm.
            msg_ = fq.recv ();

            //  If there's no message available, return immediately.
            //  The same when error occurs.
            if (msg_ == null)
                return null;

            //  Check whether the message matches at least one subscription.
            //  Non-initial parts of the message are passed 
            if (more || !options.Filter || match (msg_)) {
                more = msg_.has_more();
                return msg_;
            }

            //  Message doesn't match. Pop any remaining parts of the message
            //  from the pipe.
            while (msg_.has_more()) {
                msg_ = fq.recv ();
                Debug.Assert(msg_ != null);
            }
        }
    }
    
    protected override bool xhas_in () {
        //  There are subsequent parts of the partly-read message available.
        if (more)
            return true;

        //  If there's already a message prepared by a previous call to zmq_poll,
        //  return straight ahead.
        if (has_message)
            return true;

        //  TODO: This can result in infinite loop in the case of continuous
        //  stream of non-matching messages.
        while (true) {

            //  Get a message using fair queueing algorithm.
            message = fq.recv ();

            //  If there's no message available, return immediately.
            //  The same when error occurs.
            if (message == null) {
                Debug.Assert(ZError.IsError(ZError.EAGAIN));
                return false;
            }

            //  Check whether the message matches at least one subscription.
            if (!options.Filter || match (message)) {
                has_message = true;
                return true;
            }

            //  Message doesn't match. Pop any remaining parts of the message
            //  from the pipe.
            while (message.has_more()) {
                message = fq.recv ();
                Debug.Assert(message != null);
            }
        }

    }
    private bool match(Msg msg_) {
        return subscriptions.check(msg_.get_data());
    }

    
}
