/*
    Copyright (c) 2010-2011 250bpm s.r.o.
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

public class LB {

    //  List of outbound pipes.
    private List<Pipe> pipes;
    
    //  Number of active pipes. All the active pipes are located at the
    //  beginning of the pipes array.
    private int active;

    //  Points to the last pipe that the most recent message was sent to.
    private int current;

    //  True if last we are in the middle of a multipart message.
    private bool more;

    //  True if we are dropping current message.
    private bool dropping;
    
    public LB() {
        active = 0;
        current = 0;
        more = false;
        dropping = false;
        
        pipes = new List<Pipe>();
    }

    public void attach (Pipe pipe_) 
    {
        pipes.Add (pipe_);
        activated (pipe_);
    }

    public void terminated(Pipe pipe_) {
        int index = pipes.IndexOf (pipe_);

        //  If we are in the middle of multipart message and current pipe
        //  have disconnected, we have to drop the remainder of the message.
        if (index == current && more)
            dropping = true;

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

    public void activated(Pipe pipe_) {
        //  Move the pipe to the list of active pipes.
        Utils.swap (pipes, pipes.IndexOf (pipe_), active);
        active++;
    }

    public bool send(Msg msg_, int flags_) {
        //  Drop the message if required. If we are at the end of the message
        //  switch back to non-dropping mode.
        if (dropping) {

            more = msg_.has_more();
            dropping = more;

            msg_.close ();
            return true;
        }

        while (active > 0) {
            if (pipes[current].write (msg_))
                break;

            Debug.Assert(!more);
            active--;
            if (current < active)
                Utils.swap (pipes, current, active);
            else
                current = 0;
        }

        //  If there are no pipes we cannot send the message.
        if (active == 0) {
            ZError.errno = ZError.EAGAIN;
            return false;
        }

        //  If it's part of the message we can fluch it downstream and
        //  continue round-robinning (load balance).
        more = msg_.has_more();
        if (!more) {
            pipes[current].flush();
            if (active > 1)
                current = (current + 1) % active;
        }

        return true;
    }

    public bool has_out() {
        //  If one part of the message was already written we can definitely
        //  write the rest of the message.
        if (more)
            return true;

        while (active > 0) {

            //  Check whether a pipe has room for another message.
            if (pipes[current].check_write())
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
