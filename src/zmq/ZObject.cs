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

//  Base class for all objects that participate in inter-thread
//  communication.
public abstract class ZObject {


    //  Context provides access to the global state.
    private Ctx ctx;

    //  Thread ID of the thread the object belongs to.
    public int tid;
    
    protected ZObject(Ctx ctx, int tid) {
    	this.ctx = ctx;
    	this.tid = tid;
    }
    
    protected ZObject (ZObject parent_) :this(parent_.ctx, parent_.tid){
    	
    }    
    
    protected int get_tid ()
    {
        return tid;
    }
    
    protected Ctx get_ctx ()
    {
        return ctx;
    }
    
    public void process_command(Command cmd_) {
        switch (cmd_.type) {

        case Command.CommandType.activate_read:
            process_activate_read ();
            break;

        case Command.CommandType.activate_write:
            process_activate_write ((long)cmd_.arg);
            break;

        case Command.CommandType.stop:
            process_stop ();
            break;

        case Command.CommandType.plug:
            process_plug ();
            process_seqnum ();
            break;

        case Command.CommandType.own:
            process_own ((Own)cmd_.arg);
            process_seqnum ();
            break;

        case Command.CommandType.attach:
            process_attach ((IEngine)cmd_.arg);
            process_seqnum ();
            break;

        case Command.CommandType.bind:
            process_bind ((Pipe)cmd_.arg);
            process_seqnum ();
            break;

        case Command.CommandType.hiccup:
            process_hiccup (cmd_.arg);
            break;
    
        case Command.CommandType.pipe_term:
            process_pipe_term ();
            break;
    
        case Command.CommandType.pipe_term_ack:
            process_pipe_term_ack ();
            break;
    
        case Command.CommandType.term_req:
            process_term_req ((Own)cmd_.arg);
            break;
    
        case Command.CommandType.term:
            process_term ((int)cmd_.arg);
            break;
    
        case Command.CommandType.term_ack:
            process_term_ack ();
            break;
    
        case Command.CommandType.reap:
            process_reap ((SocketBase)cmd_.arg);
            break;
            
        case Command.CommandType.reaped:
            process_reaped ();
            break;
    
        default:
            throw new ArgumentException();
        }

    }
    
    protected bool register_endpoint (String addr_, Ctx.Endpoint endpoint_)
    {
        return ctx.register_endpoint (addr_, endpoint_);
    }
    
    protected void unregister_endpoints (SocketBase socket_)
    {
        ctx.unregister_endpoints (socket_);
    }
    
    protected Ctx.Endpoint find_endpoint (String addr_)
    {
        return ctx.find_endpoint (addr_);
    }
    
    protected void destroy_socket (SocketBase socket_)
    {
        ctx.destroy_socket (socket_);
    }

    //  Chooses least loaded I/O thread.
    protected IOThread choose_io_thread (long affinity_)
    {
        return ctx.choose_io_thread (affinity_);
    }
    
    protected void send_stop ()
    {
        //  'stop' command goes always from administrative thread to
        //  the current object. 
        Command cmd = new Command(this, Command.CommandType.stop);
        ctx.send_command (tid, cmd);
    }
    
    
    protected void send_plug (Own destination_) {
        send_plug(destination_, true);
    }

    protected void send_plug (Own destination_, bool inc_seqnum_)
    {
        if (inc_seqnum_)
            destination_.inc_seqnum ();

        Command cmd = new Command(destination_, Command.CommandType.plug);
        send_command (cmd);
    }
    
    
    protected void send_own (Own destination_, Own object_)
    {
        destination_.inc_seqnum ();
        Command cmd = new Command(destination_, Command.CommandType.own, object_);
        send_command (cmd);
    }
    
    protected void send_attach (SessionBase destination_, IEngine engine_) {
        send_attach(destination_, engine_, true);
    }
    
    protected void send_attach (SessionBase destination_,
            IEngine engine_, bool inc_seqnum_)
    {
        if (inc_seqnum_)
            destination_.inc_seqnum ();

        Command cmd = new Command(destination_, Command.CommandType.attach, engine_);
        send_command (cmd);
    }

    
    protected void send_bind (Own destination_, Pipe pipe_)
    {
        send_bind(destination_, pipe_, true);
    }
    
    protected void send_bind (Own destination_, Pipe pipe_,
            bool inc_seqnum_)
    {
        if (inc_seqnum_)
            destination_.inc_seqnum ();

        Command cmd = new Command(destination_, Command.CommandType.bind, pipe_);
        send_command (cmd);
    }


    protected void send_activate_read(Pipe destination_)
    {
        Command cmd = new Command(destination_, Command.CommandType.activate_read);
        send_command (cmd);
    }

    protected void send_activate_write (Pipe destination_,
            long msgs_read_)
    {
        Command cmd = new Command(destination_, Command.CommandType.activate_write, msgs_read_);
        send_command (cmd);
    }

    protected void send_hiccup (Pipe destination_, Object pipe_)
    {
        Command cmd = new Command(destination_, Command.CommandType.hiccup, pipe_);
        send_command (cmd);
    }

    
    protected void send_pipe_term (Pipe destination_)
    {
        Command cmd = new Command(destination_, Command.CommandType.pipe_term);
        send_command (cmd);
    }


    protected void send_pipe_term_ack (Pipe destination_)
    {
        Command cmd = new Command(destination_, Command.CommandType.pipe_term_ack);
        send_command (cmd);
    }


    protected void send_term_req (Own destination_,
            Own object_)
    {
        Command cmd = new Command(destination_, Command.CommandType.term_req, object_);
        send_command (cmd);
    }
    

    protected void send_term (Own destination_, int linger_)
    {
        Command cmd = new Command(destination_, Command.CommandType.term, linger_);
        send_command (cmd);
        
    }
    

    protected void send_term_ack (Own destination_)
    {   
        Command cmd = new Command(destination_, Command.CommandType.term_ack);
        send_command (cmd);
    }   

    protected void send_reap (SocketBase socket_)
    {
        Command cmd = new Command(ctx.get_reaper (), Command.CommandType.reap, socket_);
        send_command (cmd);
    }
    
    
    protected void send_reaped ()
    {
        Command cmd = new Command(ctx.get_reaper(), Command.CommandType.reaped);
        send_command (cmd);
    }



    protected void send_done ()
    {
        Command cmd = new Command(null, Command.CommandType.done);
        ctx.send_command(Ctx.term_tid, cmd);
    }


    protected virtual void process_stop() {
        throw new NotSupportedException();
    }
    
    protected virtual void process_plug() {
        throw new NotSupportedException();
    }


    protected virtual void process_own(Own obj)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_attach(IEngine engine)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_bind(Pipe pipe)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_activate_read()
    {
        throw new NotSupportedException();
    }

    protected virtual void process_activate_write(long msgs_read_)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_hiccup(Object hiccup_pipe)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_pipe_term()
    {
        throw new NotSupportedException();
    }

    protected virtual void process_pipe_term_ack()
    {
        throw new NotSupportedException();
    }

    protected virtual void process_term_req(Own obj)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_term(int linger)
    {
        throw new NotSupportedException();
    }


    protected virtual void process_term_ack()
    {
        throw new NotSupportedException();
    }

    protected virtual void process_reap(SocketBase socket)
    {
        throw new NotSupportedException();
    }

    protected virtual void process_reaped()
    {
        throw new NotSupportedException();
    }
    
    //  Special handler called after a command that requires a seqnum
    //  was processed. The implementation should catch up with its counter
    //  of processed commands here.
    protected virtual void process_seqnum()
    {
        throw new NotSupportedException();
    }
    
    
    private void send_command (Command cmd_)
    {
        ctx.send_command (cmd_.destination.get_tid (), cmd_);
    }

}
