/*
    Copyright (c) 2010-2011 250bpm s.r.o.
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
using NetMQ;
using System.Collections.Generic;
using System.Diagnostics;


//  Base class for objects forming a part of ownership hierarchy.
//  It handles initialisation and destruction of such objects.
abstract public class Own : ZObject {

	protected Options options;
	
    //  True if termination was already initiated. If so, we can destroy
    //  the object if there are no more child objects or pending term acks.
    private bool terminating;

    //  Sequence number of the last command sent to this object.
    private AtomicLong sent_seqnum;

    //  Sequence number of the last command processed by this object.
    private long processed_seqnum;

    //  Socket owning this object. It's responsible for shutting down
    //  this object.
    private Own owner;

    //  List of all objects owned by this socket. We are responsible
    //  for deallocating them before we quit.
    //typedef std::set <own_t*> owned_t;
    private HashSet<Own> owned;

    //  Number of events we have to get before we can destroy the object.
    private int term_acks;

    
    //  Note that the owner is unspecified in the constructor.
    //  It'll be supplied later on when the object is plugged in.

    //  The object is not living within an I/O thread. It has it's own
    //  thread outside of 0MQ infrastructure.
    public Own(Ctx parent_, int tid_)
        : base(parent_, tid_)
    {
	    terminating = false;
	    sent_seqnum = new AtomicLong(0);
	    processed_seqnum = 0;
	    owner = null;
	    term_acks = 0 ;
	    
	    options = new Options();
	    owned = new HashSet<Own>();
	}

	//  The object is living within I/O thread.
	public Own(IOThread io_thread_, Options options_) : base(io_thread_) {
	    options = options_;
	    terminating = false;
        sent_seqnum = new AtomicLong(0);
        processed_seqnum = 0;
        owner = null;
        term_acks = 0 ;
        
        owned = new HashSet<Own>();
    }
	
	abstract public void destroy();
	
    //  A place to hook in when phyicallal destruction of the object
    //  is to be delayed.
    protected virtual void process_destroy () {
        destroy();
    }

    
    private void set_owner (Own owner_)
    {
        Debug.Assert(owner == null);
        owner = owner_;
    }

	//  When another owned object wants to send command to this object
    //  it calls this function to let it know it should not shut down
    //  before the command is delivered.
    public void inc_seqnum ()
	{
	    //  This function may be called from a different thread!
	    sent_seqnum.incrementAndGet();
	}
    
    protected override void process_seqnum ()
    {
        //  Catch up with counter of processed commands.
        processed_seqnum++;

        //  We may have catched up and still have pending terms acks.
        check_term_acks ();
    }
    
    //  Launch the supplied object and become its owner.
    protected void launch_child (Own object_)
    {
        //  Specify the owner of the object.
        object_.set_owner (this);

        //  Plug the object into the I/O thread.
        send_plug (object_);

        //  Take ownership of the object.
        send_own (this, object_);
    }
    
 
	


	
	//  Terminate owned object
	protected void term_child (Own object_)
	{
	    process_term_req (object_);
	}

	
    protected override void process_term_req (Own object_)
    {
        //  When shutting down we can ignore termination requests from owned
        //  objects. The termination request was already sent to the object.
        if (terminating)
            return;

        //  If I/O object is well and alive let's ask it to terminate.

        //  If not found, we assume that termination request was already sent to
        //  the object so we can safely ignore the request.
        if (!owned.Contains(object_))
            return;

        owned.Remove(object_);
        register_term_acks (1);

        //  Note that this object is the root of the (partial shutdown) thus, its
        //  value of linger is used, rather than the value stored by the children.
        send_term (object_, options.linger);
    }


	protected override void process_own (Own object_)
	{
	    //  If the object is already being shut down, new owned objects are
	    //  immediately asked to terminate. Note that linger is set to zero.
	    if (terminating) {
	        register_term_acks (1);
	        send_term (object_, 0);
	        return;
	    }

	    //  Store the reference to the owned object.
	    owned.Add (object_);
	}

	//  Ask owner object to terminate this object. It may take a while
    //  while actual termination is started. This function should not be
    //  called more than once.
    protected void terminate ()
    {
        //  If termination is already underway, there's no point
        //  in starting it anew.
        if (terminating)
            return;

        //  As for the root of the ownership tree, there's noone to terminate it,
        //  so it has to terminate itself.
        if (owner == null) {
            process_term (options.linger);
            return;
        }

        //  If I am an owned object, I'll ask my owner to terminate me.
        send_term_req (owner, this);
    }
    
    //  Returns true if the object is in process of termination.
    protected bool is_terminating ()
    {
        return terminating;
    }

    //  Term handler is protocted rather than private so that it can
    //  be intercepted by the derived class. This is useful to add custom
    //  steps to the beginning of the termination process.
    override
    protected void process_term (int linger_)
    {
        //  Double termination should never happen.
        Debug.Assert(!terminating);

        //  Send termination request to all owned objects.
        foreach (Own it in owned)
            send_term (it, linger_);
        register_term_acks (owned.Count);
        owned.Clear ();

        //  Start termination process and check whether by chance we cannot
        //  terminate immediately.
        terminating = true;
        check_term_acks ();
    }

    //  Use following two functions to wait for arbitrary events before
    //  terminating. Just add number of events to wait for using
    //  register_tem_acks functions. When event occurs, call
    //  remove_term_ack. When number of pending acks reaches zero
    //  object will be deallocated.
    public void register_term_acks (int count_)
    {
        term_acks += count_;
    }
    
    public void unregister_term_ack() {
        Debug.Assert(term_acks > 0);
        term_acks--;

        //  This may be a last ack we are waiting for before termination...
        check_term_acks ();
    }
    

	
	override
	protected void process_term_ack ()
	{
	    unregister_term_ack ();
	    
	}


    private void check_term_acks ()
    {
        if (terminating && processed_seqnum == sent_seqnum.get () &&
              term_acks == 0) {

            //  Sanity check. There should be no active children at this point.
            Debug.Assert(owned.Count == 0);

            //  The root object has nobody to confirm the termination to.
            //  Other nodes will confirm the termination to the owner.
            if (owner != null)
                send_term_ack (owner);

            //  Deallocate the resources.
            process_destroy ();
        }
    }

	

}
