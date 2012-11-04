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

//import org.slf4j.Logger;
//import org.slf4j.LoggerFactory;

//  Note that pipe can be stored in three different arrays.
//  The array of inbound pipes (1), the array of outbound pipes (2) and
//  the generic array of pipes to deallocate (3).
public class Pipe : ZObject {

    //private static Logger LOG = LoggerFactory.getLogger(Pipe.class);
    
    public interface IPipeEvents {

        void read_activated(Pipe pipe);
        void write_activated(Pipe pipe);
        void hiccuped(Pipe pipe);
        void terminated(Pipe pipe);
        

    }
    //  Underlying pipes for both directions.
    private YPipe<Msg> inpipe;
    private YPipe<Msg> outpipe;

    //  Can the pipe be read from / written to?
    private bool in_active;
    private bool out_active;

    //  High watermark for the outbound pipe.
    private int hwm;

    //  Low watermark for the inbound pipe.
    private int lwm;

    //  Number of messages read and written so far.
    private long msgs_read;
    private long msgs_written;

    //  Last received peer's msgs_read. The actual number in the peer
    //  can be higher at the moment.
    private long peers_msgs_read;

    //  The pipe object on the other side of the pipepair.
    private Pipe peer;

    //  Sink to send events to.
    private IPipeEvents sink;
    
    //  State of the pipe endpoint. Active is common state before any
    //  termination begins. Delimited means that delimiter was read from
    //  pipe before term command was received. Pending means that term
    //  command was already received from the peer but there are still
    //  pending messages to read. Terminating means that all pending
    //  messages were already read and all we are waiting for is ack from
    //  the peer. Terminated means that 'terminate' was explicitly called
    //  by the user. Double_terminated means that user called 'terminate'
    //  and then we've got term command from the peer as well.
    enum State {
        active,
        delimited,
        pending,
        terminating,
        terminated,
        double_terminated
    } ;
    private State state;

    //  If true, we receive all the pending inbound messages before
    //  terminating. If false, we terminate immediately when the peer
    //  asks us to.
    private bool delay;

    //  Identity of the writer. Used uniquely by the reader side.
    private Blob identity;
    
    // JeroMQ only
    private ZObject parent;
    
    //  Constructor is private. Pipe can only be created using
    //  pipepair function.
	private Pipe (ZObject parent_, YPipe<Msg> inpipe_, YPipe<Msg> outpipe_,
		      int inhwm_, int outhwm_, bool delay_) : base(parent_){

		inpipe = inpipe_;
		outpipe = outpipe_;
		in_active = true;
		out_active = true;
		hwm = outhwm_;
		lwm = compute_lwm (inhwm_);
		msgs_read = 0;
		msgs_written = 0;
		peers_msgs_read = 0;
		peer = null ;
		sink = null ;
		state = State.active;
		delay = delay_;
		
		parent = parent_;
	}
	
	//  Create a pipepair for bi-directional transfer of messages.
    //  First HWM is for messages passed from first pipe to the second pipe.
    //  Second HWM is for messages passed from second pipe to the first pipe.
    //  Delay specifies how the pipe behaves when the peer terminates. If true
    //  pipe receives all the pending messages before terminating, otherwise it
    //  terminates straight away.
	public static void pipepair(ZObject[] parents_, Pipe[] pipes_, int[] hwms_,
			bool[] delays_) {
		
	    //   Creates two pipe objects. These objects are connected by two ypipes,
	    //   each to pass messages in one direction.
	            
		YPipe<Msg> upipe1 = new YPipe<Msg>(Config.message_pipe_granularity);
		YPipe<Msg> upipe2 = new YPipe<Msg>(Config.message_pipe_granularity);
	            
	    pipes_ [0] = new Pipe(parents_ [0], upipe1, upipe2,
	        hwms_ [1], hwms_ [0], delays_ [0]);
	    pipes_ [1] = new Pipe(parents_ [1], upipe2, upipe1,
	        hwms_ [0], hwms_ [1], delays_ [1]);
	            
	    pipes_ [0].set_peer (pipes_ [1]);
	    pipes_ [1].set_peer (pipes_ [0]);

	}
	
	//  Pipepair uses this function to let us know about
    //  the peer pipe object.
    private void set_peer (Pipe peer_)
    {
        //  Peer can be set once only.
        Debug.Assert(peer_ != null);
        peer = peer_;
    }
    
    //  Specifies the object to send events to.
    public void set_event_sink(IPipeEvents sink_) {
        Debug.Assert(sink == null);
        sink = sink_;
    }
    
    //  Pipe endpoint can store an opaque ID to be used by its clients.
    public void set_identity(Blob identity_) {
        identity = identity_;
    }
    
    public Blob get_identity() {
        return identity;
    }


    //  Returns true if there is at least one message to read in the pipe.
    public bool check_read() {
        if (!in_active || (state != State.active && state != State.pending))
            return false;

        //  Check if there's an item in the pipe.
        if (!inpipe.check_read ()) {
            in_active = false;
            return false;
        }

        //  If the next item in the pipe is message delimiter,
        //  initiate termination process.
        if (is_delimiter(inpipe.probe ())) {
            Msg msg = inpipe.read ();
            Debug.Assert(msg != null);
            delimit ();
            return false;
        }

        return true;
    }
    

    //  Reads a message to the underlying pipe.
    public Msg read()
    {
        if (!in_active || (state != State.active && state != State.pending))
            return null;

        Msg msg_ = inpipe.read ();

        if (msg_ == null) {
            in_active = false;
            return null;
        }

        //  If delimiter was read, start termination process of the pipe.
        if (msg_.is_delimiter ()) {
            delimit ();
            return null;
        }

        if (!msg_.has_more())
            msgs_read++;

        if (lwm > 0 && msgs_read % lwm == 0)
            send_activate_write (peer, msgs_read);

        return msg_;
    }
    
    //  Checks whether messages can be written to the pipe. If writing
    //  the message would cause high watermark the function returns false.
    public bool check_write ()
    {
        if (!out_active || state != State.active)
            return false;

        bool full = hwm > 0 && msgs_written - peers_msgs_read == (long) (hwm);

        if (full) {
            out_active = false;
            return false;
        }

        return true;
    }

    //  Writes a message to the underlying pipe. Returns false if the
    //  message cannot be written because high watermark was reached.
    public bool write (Msg msg_)
    {
        if (!check_write ())
            return false;

        bool more = msg_.has_more();
        outpipe.write (msg_, more);
        //if (LOG.isDebugEnabled()) {
        //    LOG.debug(parent.ToString() + " write " + msg_);
        //}

        if (!more)
            msgs_written++;

        return true;
    }


    //  Remove unfinished parts of the outbound message from the pipe.
    public void rollback ()
    {
        //  Remove incomplete message from the outbound pipe.
        Msg msg;
        if (outpipe!= null) {
            while ((msg = outpipe.unwrite ()) != null) {
							Debug.Assert((msg.flags & MsgFlags.More) != 0);
                //msg.close ();
            }
        }
    }
    
    //  Flush the messages downsteam.
    public void flush ()
    {
        //  The peer does not exist anymore at this point.
        if (state == State.terminating)
            return;

        if (outpipe != null && !outpipe.flush ()) {
            send_activate_read (peer);
        } 
    }


    override
    protected void process_activate_read ()
    {
        if (!in_active && (state == State.active || state == State.pending)) {
            in_active = true;
            sink.read_activated (this);
        }
    }

    override
    protected void process_activate_write (long msgs_read_)
    {
        //  Remember the peers's message sequence number.
        peers_msgs_read = msgs_read_;

        if (!out_active && state == State.active) {
            out_active = true;
            sink.write_activated (this);
        }
    }


    protected override void process_hiccup(Object pipe_)
    {
        //  Destroy old outpipe. Note that the read end of the pipe was already
        //  migrated to this thread.
        Debug.Assert(outpipe != null);
        outpipe.flush ();
        while (outpipe.read () !=null) {
        }

        //  Plug in the new outpipe.
        Debug.Assert(pipe_ != null);
        outpipe = (YPipe<Msg>) pipe_;
        out_active = true;

        //  If appropriate, notify the user about the hiccup.
        if (state == State.active)
            sink.hiccuped (this);
    }
    
    override
    protected void process_pipe_term ()
    {
        //  This is the simple case of peer-induced termination. If there are no
        //  more pending messages to read, or if the pipe was configured to drop
        //  pending messages, we can move directly to the terminating state.
        //  Otherwise we'll hang up in pending state till all the pending messages
        //  are sent.
        if (state == State.active) {
            if (!delay) {
                state = State.terminating;
                outpipe = null;
                send_pipe_term_ack (peer);
            }
            else
                state = State.pending;
            return;
        }

        //  Delimiter happened to arrive before the term command. Now we have the
        //  term command as well, so we can move straight to terminating state.
        if (state == State.delimited) {
            state = State.terminating;
            outpipe = null;
            send_pipe_term_ack (peer);
            return;
        }

        //  This is the case where both ends of the pipe are closed in parallel.
        //  We simply reply to the request by ack and continue waiting for our
        //  own ack.
        if (state == State.terminated) {
            state = State.double_terminated;
            outpipe = null;
            send_pipe_term_ack (peer);
            return;
        }

        //  pipe_term is invalid in other states.
        Debug.Assert(false);
    }
    
    override
    protected void process_pipe_term_ack ()
    {
        //  Notify the user that all the references to the pipe should be dropped.
        Debug.Assert(sink!=null);
        sink.terminated (this);

        //  In terminating and double_terminated states there's nothing to do.
        //  Simply deallocate the pipe. In terminated state we have to ack the
        //  peer before deallocating this side of the pipe. All the other states
        //  are invalid.
        if (state == State.terminated) {
            outpipe = null;
            send_pipe_term_ack (peer);
        }
        else
            Debug.Assert(state == State.terminating || state == State.double_terminated);

        //  We'll deallocate the inbound pipe, the peer will deallocate the outbound
        //  pipe (which is an inbound pipe from its point of view).
        //  First, delete all the unread messages in the pipe. We have to do it by
        //  hand because msg_t doesn't have automatic destructor. Then deallocate
        //  the ypipe itself.
        while (inpipe.read () != null) {
        }
        
        inpipe = null;

        //  Deallocate the pipe object
    }

    //  Ask pipe to terminate. The termination will happen asynchronously
    //  and user will be notified about actual deallocation by 'terminated'
    //  event. If delay is true, the pending messages will be processed
    //  before actual shutdown.
    public void terminate (bool delay_)
    {
        //  Overload the value specified at pipe creation.
        delay = delay_;

        //  If terminate was already called, we can ignore the duplicit invocation.
        if (state == State.terminated || state == State.double_terminated)
            return;

        //  If the pipe is in the phase of async termination, it's going to
        //  closed anyway. No need to do anything special here.
        else if (state == State.terminating)
            return;

        //  The simple sync termination case. Ask the peer to terminate and wait
        //  for the ack.
        else if (state == State.active) {
            send_pipe_term (peer);
            state = State.terminated;
        }

        //  There are still pending messages available, but the user calls
        //  'terminate'. We can act as if all the pending messages were read.
        else if (state == State.pending && !delay) {
            outpipe = null;
            send_pipe_term_ack (peer);
            state = State.terminating;
        }

        //  If there are pending messages still availabe, do nothing.
        else if (state == State.pending) {
        }

        //  We've already got delimiter, but not term command yet. We can ignore
        //  the delimiter and ack synchronously terminate as if we were in
        //  active state.
        else if (state == State.delimited) {
            send_pipe_term (peer);
            state = State.terminated;
        }

        //  There are no other states.
        else
            Debug.Assert(false);

        //  Stop outbound flow of messages.
        out_active = false;

        if (outpipe != null) {

            //  Drop any unfinished outbound messages.
            rollback ();

            //  Write the delimiter into the pipe. Note that watermarks are not
            //  checked; thus the delimiter can be written even when the pipe is full.
            
            Msg msg = new Msg();
            msg.init_delimiter ();
            outpipe.write (msg, false);
            flush ();
            
        }
    }
    

    //  Returns true if the message is delimiter; false otherwise.
    private static bool is_delimiter(Msg msg_) {
        return msg_.is_delimiter ();
    }

	//  Computes appropriate low watermark from the given high watermark.
	private static int compute_lwm (int hwm_)
	{
	    //  Compute the low water mark. Following point should be taken
	    //  into consideration:
	    //
	    //  1. LWM has to be less than HWM.
	    //  2. LWM cannot be set to very low value (such as zero) as after filling
	    //     the queue it would start to refill only after all the messages are
	    //     read from it and thus unnecessarily hold the progress back.
	    //  3. LWM cannot be set to very high value (such as HWM-1) as it would
	    //     result in lock-step filling of the queue - if a single message is
	    //     read from a full queue, writer thread is resumed to write exactly one
	    //     message to the queue and go back to sleep immediately. This would
	    //     result in low performance.
	    //
	    //  Given the 3. it would be good to keep HWM and LWM as far apart as
	    //  possible to reduce the thread switching overhead to almost zero,
	    //  say HWM-LWM should be max_wm_delta.
	    //
	    //  That done, we still we have to account for the cases where
	    //  HWM < max_wm_delta thus driving LWM to negative numbers.
	    //  Let's make LWM 1/2 of HWM in such cases.
	    int result = (hwm_ > Config.max_wm_delta * 2) ?
	        hwm_ - Config.max_wm_delta : (hwm_ + 1) / 2;

	    return result;
	}
	

    //  Handler for delimiter read from the pipe.
	private void delimit ()
	{
	    if (state == State.active) {
	        state = State.delimited;
	        return;
	    }

	    if (state == State.pending) {
	        outpipe = null;
	        send_pipe_term_ack (peer);
	        state = State.terminating;
	        return;
	    }

	    //  Delimiter in any other state is invalid.
	    Debug.Assert(false);
	}

 
    //  Temporaraily disconnects the inbound message stream and drops
    //  all the messages on the fly. Causes 'hiccuped' event to be generated
    //  in the peer.
    public void hiccup() {
        //  If termination is already under way do nothing.
        if (state != State.active)
            return;

        //  We'll drop the pointer to the inpipe. From now on, the peer is
        //  responsible for deallocating it.
        inpipe = null;

        //  Create new inpipe.
        inpipe = new YPipe<Msg>(Config.message_pipe_granularity);
        in_active = true;

        //  Notify the peer about the hiccup.
        send_hiccup (peer, inpipe);

    }



    
    public override String ToString() {
        return base.ToString() + "[" + parent + "]";
    }


}
