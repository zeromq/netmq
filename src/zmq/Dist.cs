/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2011 Other contributors as noted in the AUTHORS file


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

public class Dist {
    //  List of outbound pipes.
    //typedef array_t <zmq::pipe_t, 2> pipes_t;
    private List<Pipe> pipes;

    //  Number of all the pipes to send the next message to.
    private int matching;

    //  Number of active pipes. All the active pipes are located at the
    //  beginning of the pipes array. These are the pipes the messages
    //  can be sent to at the moment.
    private int active;

    //  Number of pipes eligible for sending messages to. This includes all
    //  the active pipes plus all the pipes that we can in theory send
    //  messages to (the HWM is not yet reached), but sending a message
    //  to them would result in partial message being delivered, ie. message
    //  with initial parts missing.
    private int eligible;

    //  True if last we are in the middle of a multipart message.
    private bool more;
    
    public Dist() {
    	matching = 0;
    	active = 0;
    	eligible = 0;
    	more = false;
    	pipes = new List<Pipe>();
    }
    
    //  Adds the pipe to the distributor object.
    public void attach (Pipe pipe_)
    {   
        //  If we are in the middle of sending a message, we'll add new pipe
        //  into the list of eligible pipes. Otherwise we add it to the list
        //  of active pipes. 
        if (more) {
            pipes.Add (pipe_); 
            //pipes.swap (eligible, pipes.size () - 1);
            Utils.swap(pipes, eligible, pipes.Count - 1);
            eligible++;
        }
        else {
            pipes.Add (pipe_);
            //pipes.swap (active, pipes.size () - 1);
            Utils.swap(pipes, active, pipes.Count - 1);
            active++;
            eligible++;
        }
    }
    
    //  Mark the pipe as matching. Subsequent call to send_to_matching
    //  will send message also to this pipe.
    public void match(Pipe pipe_) {
        
        int idx = pipes.IndexOf (pipe_);
        //  If pipe is already matching do nothing.
        if (idx < matching)
            return;

        //  If the pipe isn't eligible, ignore it.
        if (idx >= eligible)
            return;

        //  Mark the pipe as matching.
        Utils.swap( pipes, idx, matching);
        matching++;

    }
    

    //  Mark all pipes as non-matching.
    public void unmatch() {
        matching = 0;
    }


    //  Removes the pipe from the distributor object.
    public void terminated(Pipe pipe_) {
        //  Remove the pipe from the list; adjust number of matching, active and/or
        //  eligible pipes accordingly.
        if (pipes.IndexOf (pipe_) < matching)
            matching--;
        if (pipes.IndexOf (pipe_) < active)
            active--;
        if (pipes.IndexOf (pipe_) < eligible)
            eligible--;
        pipes.Remove(pipe_);
    }

    //  Activates pipe that have previously reached high watermark.
    public void activated(Pipe pipe_) {
        //  Move the pipe from passive to eligible state.
        Utils.swap (pipes, pipes.IndexOf (pipe_), eligible);
        eligible++;

        //  If there's no message being sent at the moment, move it to
        //  the active state.
        if (!more) {
            Utils.swap (pipes, eligible - 1, active);
            active++;
        }

    }

    //  Send the message to all the outbound pipes.
		public bool send_to_all(Msg msg_, ZmqSendRecieveOptions flags_)
		{
        matching = active;
        return send_to_matching (msg_, flags_);
    }

    //  Send the message to the matching outbound pipes.
		public bool send_to_matching(Msg msg_, ZmqSendRecieveOptions flags_)
		{
        //  Is this end of a multipart message?
        bool msg_more = msg_.has_more();

        //  Push the message to matching pipes.
        distribute (msg_, flags_);

        //  If mutlipart message is fully sent, activate all the eligible pipes.
        if (!msg_more)
            active = eligible;

        more = msg_more;

        return true;

    }

    //  Put the message to all active pipes.
		private void distribute(Msg msg_, ZmqSendRecieveOptions flags_)
		{
        //  If there are no matching pipes available, simply drop the message.
        if (matching == 0) {
            return;
        }
        
        for (int i = 0; i < matching; ++i)
            if(!write (pipes[i], msg_))
                --i; //  Retry last write because index will have been swapped
        return;
        
    }
    
    public bool has_out ()
    {
        return true;
    }

    //  Write the message to the pipe. Make the pipe inactive if writing
    //  fails. In such a case false is returned.
    private bool write (Pipe pipe_, Msg msg_)
    {
        if (!pipe_.write (msg_)) {
            Utils.swap(pipes, pipes.IndexOf (pipe_), matching - 1);
            matching--;
            Utils.swap(pipes, pipes.IndexOf (pipe_), active - 1);
            active--;
            Utils.swap(pipes, active, eligible - 1);
            eligible--;
            return false;
        }
        if (!msg_.has_more())
            pipe_.flush ();
        return true;
    }




}
