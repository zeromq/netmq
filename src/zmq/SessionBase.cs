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
using Wintellect.PowerCollections;

public class SessionBase : Own ,
                        Pipe.IPipeEvents, IPollEvents, 
                        IMsgSink, IMsgSource 
{
    //  If true, this session (re)connects to the peer. Otherwise, it's
    //  a transient session created by the listener.
    private bool connect;
    
    //  Pipe connecting the session to its socket.
    private Pipe pipe;

    //  This set is added to with pipes we are disconnecting, but haven't yet completed
    private HashSet<Pipe> terminating_pipes;
    
    //  This flag is true if the remainder of the message being processed
    //  is still in the in pipe.
    private bool incomplete_in;

    //  True if termination have been suspended to push the pending
    //  messages to the network.
    private bool pending;

    //  The protocol I/O engine connected to the session.
    private IEngine engine;

    //  The socket the session belongs to.
    private SocketBase socket;

    //  I/O thread the session is living in. It will be used to plug in
    //  the engines into the same thread.
    private IOThread io_thread;

    //  ID of the linger timer
    private static int linger_timer_id = 0x20;

    //  True is linger timer is running.
    private bool has_linger_timer;

    //  If true, identity has been sent/received from the network.
    private bool identity_sent;
    private bool identity_received;
    
    //  Protocol and address to use when connecting.
    private Address addr;

    private IOObject io_object;

    public static SessionBase create(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_, Address addr_) {
        
        SessionBase s = null;
        switch (options_.type) {
        case ZMQ.ZMQ_REQ:
            s = new  Req.ReqSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_DEALER:
            s = new Dealer.DealerSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_REP:
            s = new Rep.RepSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_ROUTER:
            s = new Router.RouterSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_PUB:
            s = new Pub.PubSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_XPUB:
            s = new XPub.XPubSession(io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_SUB:
            s = new  Sub.SubSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_XSUB:
            s = new XSub.XSubSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
            
        case ZMQ.ZMQ_PUSH:
            s = new Push.PushSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_PULL:
            s = new Pull.PullSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        case ZMQ.ZMQ_PAIR:
            s = new Pair.PairSession (io_thread_, connect_,
                socket_, options_, addr_);
            break;
        default:
            throw new ArgumentException("type=" + options_.type);
        }
        return s;
    }

    public SessionBase(IOThread io_thread_, bool connect_,
            SocketBase socket_, Options options_, Address addr_) : base(io_thread_, options_) {
        io_object = new IOObject(io_thread_);
        
        connect = connect_;
        pipe = null;
        incomplete_in = false;
        pending = false;
        engine = null;
        socket = socket_;
        io_thread = io_thread_;
        has_linger_timer = false;
        identity_sent = false;
        identity_received = false;
        addr = addr_;
     
        terminating_pipes = new HashSet <Pipe> ();
    }

    public override void destroy () {
        Debug.Assert(pipe == null);

        //  If there's still a pending linger timer, remove it.
        if (has_linger_timer) {
            io_object.cancel_timer (linger_timer_id);
            has_linger_timer = false;
        }

        //  Close the engine.
        if (engine != null)
            engine.terminate ();
        
    }


    //  To be used once only, when creating the session.
    public void attach_pipe(Pipe pipe_) {
        Debug.Assert(!is_terminating ());
        Debug.Assert(pipe == null);
        Debug.Assert(pipe_ != null);
        pipe = pipe_;
        pipe.set_event_sink (this);
    }
    
    public virtual Msg pull_msg () 
    {
        
        Msg msg_ = null;
        
        //  First message to send is identity
        if (!identity_sent) {
            msg_ = new Msg(options.identity_size);
            msg_.put(options.identity, 0, options.identity_size);
            identity_sent = true;
            incomplete_in = false;
            
            return msg_;
        }

        if (pipe == null || (msg_ = pipe.read ()) == null ) {
            return null;
        }
        incomplete_in = msg_.has_more();

        return msg_;

    }
    
    public virtual bool push_msg (Msg msg_)
    {
        //  First message to receive is identity (if required).
        if (!identity_received) {
            msg_.SetFlags (Msg.identity);
            identity_received = true;
            
            if (!options.recv_identity) {
                return true;
            }
        }
        
        if (pipe != null && pipe.write (msg_)) {
            return true;
        }

        ZError.errno = (ZError.EAGAIN);
        return false;
    }
    

    protected virtual void reset() {
        //  Restore identity flags.
        identity_sent = false;
        identity_received = false;
    }
    

    public void flush() {
        if (pipe != null)
            pipe.flush ();
    }
    

    //  Remove any half processed messages. Flush unflushed messages.
    //  Call this function when engine disconnect to get rid of leftovers.
    private void clean_pipes() 
    {
        if (pipe != null) {

            //  Get rid of half-processed messages in the out pipe. Flush any
            //  unflushed messages upstream.
            pipe.rollback ();
            pipe.flush ();

            //  Remove any half-read message from the in pipe.
            while (incomplete_in) {
                Msg msg = pull_msg ();
                if (msg == null) {
                    Debug.Assert(!incomplete_in);
                    break;
                }
                msg.close ();
            }
        }
    }

    public void terminated(Pipe pipe_) 
    {
        //  Drop the reference to the deallocated pipe.
        Debug.Assert(pipe == pipe_ || terminating_pipes.Contains (pipe_));
        
        if (pipe == pipe_)
            // If this is our current pipe, remove it
            pipe = null;
        else
            // Remove the pipe from the detached pipes set
            terminating_pipes.Remove (pipe_);
        
        //  If we are waiting for pending messages to be sent, at this point
        //  we are sure that there will be no more messages and we can proceed
        //  with termination safely.
        if (pending && pipe == null && terminating_pipes.Count == 0)
            proceed_with_term ();
    }

    public void read_activated(Pipe pipe_) 
    {
        // Skip activating if we're detaching this pipe
        if (pipe != pipe_) {
            Debug.Assert(terminating_pipes.Contains (pipe_));
            return;
        }
        
        if (engine != null)
            engine.activate_out ();
        else
            pipe.check_read ();
    }
    
    public void write_activated (Pipe pipe_)
    {
        // Skip activating if we're detaching this pipe
        if (pipe != pipe_) {
            Debug.Assert(terminating_pipes.Contains (pipe_));
            return;
        }


        if (engine != null)
            engine.activate_in ();
    }

    public void hiccuped (Pipe pipe_)
    {
        //  Hiccups are always sent from session to socket, not the other
        //  way round.
        throw new NotSupportedException("Must Override");

    }

    public SocketBase get_soket ()
    {
        return socket;
    }

    protected override void process_plug ()
    {
        io_object.set_handler(this);
        if (connect)
            start_connecting (false);
    }


    protected override void process_attach (IEngine engine_)
    {
        Debug.Assert(engine_ != null);

        //  Create the pipe if it does not exist yet.
        if (pipe == null && !is_terminating ()) {
            ZObject[] parents = {this, socket};
            Pipe[] pipes = {null, null};
            int[] hwms = {options.rcvhwm, options.sndhwm};
            bool[] delays = {options.delay_on_close, options.delay_on_disconnect};
            Pipe.pipepair (parents, pipes, hwms, delays);

            //  Plug the local end of the pipe.
            pipes [0].set_event_sink (this);

            //  Remember the local end of the pipe.
            Debug.Assert(pipe == null);
            pipe = pipes [0];

            //  Ask socket to plug into the remote end of the pipe.
            send_bind (socket, pipes [1]);
        }

        //  Plug in the engine.
        Debug.Assert(engine == null);
        engine = engine_;
        engine.plug (io_thread, this);
    }
    
    public void detach() 
    {
        //  Engine is dead. Let's forget about it.
        engine = null;

        //  Remove any half-done messages from the pipes.
        clean_pipes ();

        //  Send the event to the derived class.
        detached ();

        //  Just in case there's only a delimiter in the pipe.
        if (pipe != null)
            pipe.check_read ();
    }
    
    protected override void process_term (int linger_)
    {
        Debug.Assert(!pending);

        //  If the termination of the pipe happens before the term command is
        //  delivered there's nothing much to do. We can proceed with the
        //  stadard termination immediately.
        if (pipe == null) {
            proceed_with_term ();
            return;
        }

        pending = true;

        //  If there's finite linger value, delay the termination.
        //  If linger is infinite (negative) we don't even have to set
        //  the timer.
        if (linger_ > 0) {
            Debug.Assert(!has_linger_timer);
            io_object.add_timer (linger_, linger_timer_id);
            has_linger_timer = true;
        }

        //  Start pipe termination process. Delay the termination till all messages
        //  are processed in case the linger time is non-zero.
        pipe.terminate (linger_ != 0);

        //  TODO: Should this go into pipe_t::terminate ?
        //  In case there's no engine and there's only delimiter in the
        //  pipe it wouldn't be ever read. Thus we check for it explicitly.
        pipe.check_read ();
    }
    

    //  Call this function to move on with the delayed process_term.
    private void proceed_with_term() {
        //  The pending phase have just ended.
        pending = false;

        //  Continue with standard termination.
        base.process_term (0);
    }


    public void timer_event(int id_) {
        
        //  Linger period expired. We can proceed with termination even though
        //  there are still pending messages to be sent.
        Debug.Assert(id_ == linger_timer_id);
        has_linger_timer = false;

        //  Ask pipe to terminate even though there may be pending messages in it.
        Debug.Assert(pipe != null);
        pipe.terminate (false);
    }


    private void detached() {
        //  Transient session self-destructs after peer disconnects.
        if (!connect) {
            terminate ();
            return;
        }

        //  For delayed connect situations, terminate the pipe
        //  and reestablish later on
        if (pipe != null && options.delay_attach_on_connect == 1
            && addr.protocol  != "pgm" && addr.protocol != "epgm") {
            pipe.hiccup ();
            pipe.terminate (false);
            terminating_pipes.Add (pipe);
            pipe = null;
        }
        
        reset ();

        //  Reconnect.
        if (options.reconnect_ivl != -1)
            start_connecting (true);

        //  For subscriber sockets we hiccup the inbound pipe, which will cause
        //  the socket object to resend all the subscriptions.
        if (pipe != null && (options.type == ZMQ.ZMQ_SUB || options.type == ZMQ.ZMQ_XSUB))
            pipe.hiccup ();

    }

    
    private void start_connecting (bool wait_)
    {
        Debug.Assert(connect);

        //  Choose I/O thread to run connecter in. Given that we are already
        //  running in an I/O thread, there must be at least one available.
        IOThread io_thread = choose_io_thread (options.affinity);
        Debug.Assert(io_thread != null);

        //  Create the connecter object.

        if (addr.protocol.Equals("tcp")) {
            TcpConnecter connecter = new TcpConnecter (
                io_thread, this, options, addr, wait_);
            //alloc_Debug.Assert(connecter);
            launch_child (connecter);
            return;
        }
        
        if (addr.protocol.Equals("ipc")) {
            IpcConnecter connecter = new IpcConnecter (
                io_thread, this, options, addr, wait_);
            //alloc_Debug.Assert(connecter);
            launch_child (connecter);
            return;
        }
        
        Debug.Assert(false);
    }
    
    public override String ToString() {
        return base.ToString() + "[" + options.socket_id + "]";
    }

    public virtual void in_event() {
        throw new NotSupportedException();
        
    }

    public virtual void out_event()
    {
        throw new NotSupportedException();
        
    }

    



}
