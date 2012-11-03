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
using System.Collections.Generic;
using System.Diagnostics;

//  Class manages a set of inbound pipes. On receive it performs fair
//  queueing so that senders gone berserk won't cause denial of
//  service for decent senders.
public class FQ {

    //  Inbound pipes.
    private List<Pipe> pipes;
    
    //  Number of active pipes. All the active pipes are located at the
    //  beginning of the pipes array.
    private int active;

    //  Index of the next bound pipe to read a message from.
    private int current;

    //  If true, part of a multipart message was already received, but
    //  there are following parts still waiting in the current pipe.
    private bool more;
    
    public FQ () {
        active = 0;
        current = 0;
        more = false;
        
        pipes = new List<Pipe>();
    }
    
    public void attach (Pipe pipe_)
    {
        pipes.Add (pipe_);
        Utils.swap (pipes, active, pipes.Count - 1);
        active++;
    }
    
    public void terminated (Pipe pipe_)
    {
        int index = pipes.IndexOf (pipe_);

        //  Remove the pipe from the list; adjust number of active pipes
        //  accordingly.
        if (index < active) {
            active--;
            Utils.swap (pipes, index, active);
            if (current == active)
                current = 0;
        }
        pipes.Remove (pipe_);
    }

    public void activated (Pipe pipe_)
    {
        //  Move the pipe to the list of active pipes.
        Utils.swap(pipes, pipes.IndexOf (pipe_), active);
        active++;
    }

    public Msg recv ()
    {
        return recvpipe (null);
    }

    public Msg recvpipe(Pipe[] pipe_) {
        //  Deallocate old content of the message.
        Msg msg_;

        //  Round-robin over the pipes to get the next message.
        while (active > 0) {

            //  Try to fetch new message. If we've already read part of the message
            //  subsequent part should be immediately available.
            msg_ = pipes[current].read ();

            bool fetched = msg_ != null;

            //  Note that when message is not fetched, current pipe is deactivated
            //  and replaced by another active pipe. Thus we don't have to increase
            //  the 'current' pointer.
            if (fetched) {
                if (pipe_ != null)
                    pipe_[0] = pipes[current];
                more = msg_.has_more();
                if (!more)
                    current = (current + 1) % active;
                return msg_;
            }
            
            //  Check the atomicity of the message.
            //  If we've already received the first part of the message
            //  we should get the remaining parts without blocking.
            Debug.Assert(!more);
            
            active--;
            Utils.swap (pipes, current, active);
            if (current == active)
                current = 0;
        }

        //  No message is available. Initialise the output parameter
        //  to be a 0-byte message.
        ZError.errno = ZError.EAGAIN;
        return null;
    }

    public bool has_in ()
    {
        //  There are subsequent parts of the partly-read message available.
        if (more)
            return true;

        //  Note that messing with current doesn't break the fairness of fair
        //  queueing algorithm. If there are no messages available current will
        //  get back to its original value. Otherwise it'll point to the first
        //  pipe holding messages, skipping only pipes with no messages available.
        while (active > 0) {
            if (pipes[current].check_read ())
                return true;

            //  Deactivate the pipe.
            active--;
            Utils.swap (pipes, current, active);
            if (current == active)
                current = 0;
        }

        return false;
    }

}
