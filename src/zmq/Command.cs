/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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

//  This structure defines the commands that can be sent between threads.
public class Command {

    //  Object to process the command.
    //private ZObject destination;
    //private Type type;
    
    public enum CommandType {
        //  Sent to I/O thread to let it know that it should
        //  terminate itself.
        stop,
        //  Sent to I/O object to make it register with its I/O thread
        plug,
        //  Sent to socket to let it know about the newly created object.
        own,
        //  Attach the engine to the session. If engine is NULL, it informs
        //  session that the connection have failed.
        attach,
        //  Sent from session to socket to establish pipe(s) between them.
        //  Caller have used inc_seqnum beforehand sending the command.
        bind,
        //  Sent by pipe writer to inform dormant pipe reader that there
        //  are messages in the pipe.
        activate_read,
        //  Sent by pipe reader to inform pipe writer about how many
        //  messages it has read so far.
        activate_write,
        //  Sent by pipe reader to writer after creating a new inpipe.
        //  The parameter is actually of type pipe_t::upipe_t, however,
        //  its definition is private so we'll have to do with void*.
        hiccup,
        //  Sent by pipe reader to pipe writer to ask it to terminate
        //  its end of the pipe.
        pipe_term,
        //  Pipe writer acknowledges pipe_term command.
        pipe_term_ack,
        //  Sent by I/O object ot the socket to request the shutdown of
        //  the I/O object.
        term_req,
        //  Sent by socket to I/O object to start its shutdown.
        term,
        //  Sent by I/O object to the socket to acknowledge it has
        //  shut down.
        term_ack,
        //  Transfers the ownership of the closed socket
        //  to the reaper thread.
        reap,
        //  Closed socket notifies the reaper that it's already deallocated.
        reaped,
        //  Sent by reaper thread to the term thread when all the sockets
        //  are successfully deallocated.
        done        
    }
    

    public Command () {
    }

    public Command(ZObject destination, CommandType type)
        : this(destination, type, null)
    {
        
    }

    public Command(ZObject destination, CommandType type, Object arg)
    {
        this.destination = destination;
        this.type = type;
        this.arg = arg;
    }
    
    public ZObject destination {
        get; private set;
    }

    public CommandType type
    {
        get; private set;
    }

    public Object arg { get; private set; }
    
    public override String ToString() {
        return base.ToString() + "[" + type + ", " + destination + "]";
    }

}
