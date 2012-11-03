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
using System.Net.Sockets;
using System.Diagnostics;

public abstract class SocketBase : Own ,IPollEvents, Pipe.IPipeEvents {
    
    //private static Logger LOG = LoggerFactory.getLogger(SocketBase.class);
    
    private Dictionary<String, Own> endpoints;

    //  Used to check whether the object is a socket.
    private uint tag;

    //  If true, associated context was already terminated.
    private bool ctx_terminated;

    //  If true, object should have been already destroyed. However,
    //  destruction is delayed while we unwind the stack to the point
    //  where it doesn't intersect the object being destroyed.
    private bool destroyed;

    //  Socket's mailbox object.
    private Mailbox mailbox;

    //  List of attached pipes.
    //typedef array_t <pipe_t, 3> pipes_t;
    private List<Pipe> pipes;

    //  Reaper's poller and handle of this socket within it.
    private Poller poller;
    private Socket handle;
    

    //  Timestamp of when commands were processed the last time.
    private long last_tsc;

    //  Number of messages received since last command processing.
    private int ticks;

    //  True if the last message received had MORE flag set.
    private bool rcvmore;

    // Monitor socket
    private SocketBase monitor_socket;

    // Bitmask of events being monitored
    private int monitor_events;

    protected SocketBase (Ctx parent_, int tid_, int sid_) :base(parent_, tid_)
    { 
        tag = 0xbaddecaf;
        ctx_terminated = false;
        destroyed = false;
        last_tsc = 0;
        ticks = 0;
        rcvmore = false;
        monitor_socket = null;
        monitor_events = 0;
        
        options.socket_id = sid_;
        
        endpoints = new Dictionary<string,Own>();
        pipes = new List<Pipe>();
        
        mailbox = new Mailbox("socket-" + sid_);
    }
    
    //  Concrete algorithms for the x- methods are to be defined by
    //  individual socket types.
    abstract protected void xattach_pipe (Pipe pipe_, bool icanhasall_);
    abstract protected void xterminated(Pipe pipe_); 

    
    //  Returns false if object is not a socket.
    public bool check_tag ()
    {
        return tag == 0xbaddecaf;
    }

    
    //  Create a socket of a specified type.
    public static SocketBase create (int type_, Ctx parent_,
        int tid_, int sid_)
    {
        SocketBase s = null;
        switch (type_) {

        case ZMQ.ZMQ_PAIR:
            s = new Pair (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_PUB:
            s = new Pub (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_SUB:
            s = new Sub (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_REQ:
            s = new Req (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_REP:
            s = new Rep (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_DEALER:
            s = new Dealer (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_ROUTER:
            s = new Router (parent_, tid_, sid_);
            break;     
        case ZMQ.ZMQ_PULL:
            s = new Pull (parent_, tid_, sid_);
            break;
        case ZMQ.ZMQ_PUSH:
            s = new Push (parent_, tid_, sid_);
            break;
            
        case ZMQ.ZMQ_XPUB:
            s = new XPub (parent_, tid_, sid_);
            break;
            
        case ZMQ.ZMQ_XSUB:
            s = new XSub (parent_, tid_, sid_);
            break;
        
        default:
            throw new ArgumentException ("type=" + type_);
        }
        return s;
    }
    
    public override void destroy () {
        stop_monitor ();
        
        Debug.Assert(destroyed);
    }

    //  Returns the mailbox associated with this socket.
    public Mailbox get_mailbox () {
        return mailbox;
    }
    
    //  Interrupt blocking call if the socket is stuck in one.
    //  This function can be called from a different thread!
    public void stop() {
        //  Called by ctx when it is terminated (zmq_term).
        //  'stop' command is sent from the threads that called zmq_term to
        //  the thread owning the socket. This way, blocking call in the
        //  owner thread can be interrupted.
        send_stop ();
        
    }
    
    //  Check whether transport protocol, as specified in connect or
    //  bind, is available and compatible with the socket type.
    private void check_protocol (String protocol_)
    {
        //  First check out whether the protcol is something we are aware of.
        if (!protocol_.Equals("inproc") && 
            !protocol_.Equals("ipc") && !protocol_.Equals("tcp") /*&&
              !protocol_.equals("pgm") && !protocol_.equals("epgm")*/) {
            ZError.errno = (ZError.EPROTONOSUPPORT);
            throw new NotSupportedException(protocol_);
        }

        //  Check whether socket type and transport protocol match.
        //  Specifically, multicast protocols can't be combined with
        //  bi-directional messaging patterns (socket types).
        if ((protocol_.Equals("pgm") || protocol_.Equals("epgm")) &&
              options.type != ZMQ.ZMQ_PUB && options.type != ZMQ.ZMQ_SUB &&
              options.type != ZMQ.ZMQ_XPUB && options.type != ZMQ.ZMQ_XSUB) {
            ZError.errno = (ZError.EPROTONOSUPPORT);
            throw new NotSupportedException (protocol_ + ",type=" + options.type);
        }

        //  Protocol is available.
    }
    

    //  Register the pipe with this socket.
    private void attach_pipe (Pipe pipe_) {
        attach_pipe(pipe_, false);
    }
    
    private void attach_pipe (Pipe pipe_, bool icanhasall_)
    {
        //  First, register the pipe so that we can terminate it later on.
        
        pipe_.set_event_sink (this);
        pipes.Add (pipe_);

        //  Let the derived socket type know about new pipe.
        xattach_pipe (pipe_, icanhasall_);

        //  If the socket is already being closed, ask any new pipes to terminate
        //  straight away.
        if (is_terminating ()) {
            register_term_acks (1);
            pipe_.terminate (false);
        }
    }
    
    public bool setsockopt(int option_, Object optval_) {
        
        if (ctx_terminated) {
            ZError.errno = ZError.ETERM;
            return false;
        }

        //  First, check whether specific socket type overloads the option.
        bool rc = xsetsockopt (option_, optval_);
        if (rc || !ZError.IsError(ZError.EINVAL))
            return false;

        //  If the socket type doesn't support the option, pass it to
        //  the generic option parser.
        ZError.clear();
        return options.setsockopt (option_, optval_);
    }
    
    public int getsockopt(int option_) {
        
        if (ctx_terminated) {
            ZError.errno=(ZError.ETERM);
            return -1;
        }
        
        if (option_ == ZMQ.ZMQ_RCVMORE) {
            return rcvmore ? 1 : 0;
        }
        if (option_ == ZMQ.ZMQ_EVENTS) {
            bool rc = process_commands (0, false);
            if (!rc && (ZError.IsError(ZError.EINTR) || ZError.IsError(ZError.ETERM)))
                return -1;
            Debug.Assert(rc);
            int val = 0;
            if (has_out ())
                val |= ZMQ.ZMQ_POLLOUT;
            if (has_in ())
                val |= ZMQ.ZMQ_POLLIN;
            return val;
        }
        
        return (int) getsockoptx(option_);
    }
    
    public Object getsockoptx(int option_) {
        if (ctx_terminated) {
            ZError.errno = (ZError.ETERM);
            return null;
        }

        if (option_ == ZMQ.ZMQ_RCVMORE) {
            return rcvmore ? 1 : 0;
        }
        
        if (option_ == ZMQ.ZMQ_FD) {
            return mailbox.get_fd();
        }
        
        if (option_ == ZMQ.ZMQ_EVENTS) {
            bool rc = process_commands (0, false);
            if (!rc && (ZError.IsError(ZError.EINTR) || ZError.IsError(ZError.ETERM)))
                return -1;
            Debug.Assert(rc);
            int val = 0;
            if (has_out ())
                val |= ZMQ.ZMQ_POLLOUT;
            if (has_in ())
                val |= ZMQ.ZMQ_POLLIN;
            return val;
        }
        //  If the socket type doesn't support the option, pass it to
        //  the generic option parser.
        return options.getsockopt (option_);

    }
    
    public bool bind (String addr_) {
        if (ctx_terminated) {
            ZError.errno = (ZError.ETERM);
            return false;
        }

        //  Process pending commands, if any.
        bool brc = process_commands (0, false);
        if (!brc)
            return false;

        //  Parse addr_ string.
        Uri uri;
        try {
            uri = new Uri(addr_);
        } catch (Exception e) {
            throw new ArgumentException(addr_,e);
        }
        String protocol = uri.Scheme;
        String address = uri.Authority;
        String path = uri.AbsolutePath;
        if (address == null)
            address = path;

        check_protocol (protocol);

        if (protocol.Equals("inproc")) {
            Ctx.Endpoint endpoint = new Ctx.Endpoint(this, options);
            bool rc = register_endpoint (addr_, endpoint);
            if (rc) {
                // Save last endpoint URI
                options.last_endpoint = addr_;
            }
            return rc;
        }
        if (protocol.Equals("pgm") || protocol.Equals("epgm")) {
            //  For convenience's sake, bind can be used interchageable with
            //  connect for PGM and EPGM transports.
            return connect (addr_);
        }

        //  Remaining trasnports require to be run in an I/O thread, so at this
        //  point we'll choose one.
        IOThread io_thread = choose_io_thread (options.affinity);
        if (io_thread == null) {
            ZError.errno = (ZError.EMTHREAD);
            return false;
        }

        if (protocol.Equals("tcp")) {
            TcpListener listener = new TcpListener (
                io_thread, this, options);
            bool rc = listener.set_address (address);
            if (!rc) {
                listener.destroy();
                event_bind_failed (addr_, ZError.errno);
                //LOG.error("Failed to Bind", ZError.exc());
                return false;
            }

            // Save last endpoint URI
            options.last_endpoint = listener.get_address ();

            add_endpoint (addr_, listener);
            return true;
        }

        if (protocol.Equals("ipc")) {
            IpcListener listener = new IpcListener (
                io_thread, this, options);
            bool rc = listener.set_address (address);
            if (!rc) {
                listener.destroy();
                event_bind_failed (addr_, ZError.errno);
                return false;
            }

            // Save last endpoint URI
            options.last_endpoint = listener.get_address ();

            add_endpoint (addr_, listener);
            return true;
        }

        Debug.Assert(false);
        return false;
    }

    public bool connect (String addr_)
    {
        if (ctx_terminated) {
            ZError.errno = (ZError.ETERM);
            return false;
        }

        //  Process pending commands, if any.
        bool brc = process_commands (0, false);
        if (!brc)
            return false;

        //  Parse addr_ string.
        Uri uri;
        try {
            uri = new Uri(addr_);
        } catch (Exception e) {
            throw new ArgumentException(addr_,e);
        }
        
        String protocol = uri.Scheme;
        String address = uri.Authority;
        String path = uri.AbsolutePath;
        if (address == null)
            address = path;

        check_protocol (protocol);

        if (protocol.Equals("inproc")) {

            //  TODO: inproc connect is specific with respect to creating pipes
            //  as there's no 'reconnect' functionality implemented. Once that
            //  is in place we should follow generic pipe creation algorithm.

            //  Find the peer endpoint.
            Ctx.Endpoint peer = find_endpoint (addr_);
            if (peer.socket == null)
                return false;
            // The total HWM for an inproc connection should be the sum of
            // the binder's HWM and the connector's HWM.
            int  sndhwm;
            int  rcvhwm;
            if (options.sndhwm == 0 || peer.options.rcvhwm == 0)
                sndhwm = 0;
            else
                sndhwm = options.sndhwm + peer.options.rcvhwm;
            if (options.rcvhwm == 0 || peer.options.sndhwm == 0)
                rcvhwm = 0;
            else
                rcvhwm = options.rcvhwm + peer.options.sndhwm;

            //  Create a bi-directional pipe to connect the peers.
            ZObject[] parents = {this, peer.socket};
            Pipe[] pipes = {null, null};
            int[] hwms = {sndhwm, rcvhwm};
            bool[] delays = {options.delay_on_disconnect, options.delay_on_close};
            Pipe.pipepair (parents, pipes, hwms, delays);

            //  Attach local end of the pipe to this socket object.
            attach_pipe (pipes [0]);

            //  If required, send the identity of the peer to the local socket.
            if (peer.options.recv_identity) {
                Msg id = new Msg(peer.options.identity_size);
                id.put(peer.options.identity, 0 , peer.options.identity_size);
                id.SetFlags (Msg.identity);
                bool written = pipes [0].write (id);
                Debug.Assert(written);
                pipes [0].flush ();
            }
            
            //  If required, send the identity of the local socket to the peer.
            if (options.recv_identity) {
                Msg id = new Msg(options.identity_size);
                id.put(options.identity, 0 , options.identity_size);
                id.SetFlags (Msg.identity);
                bool written = pipes [1].write (id);
                Debug.Assert(written);
                pipes [1].flush ();
            }

            //  Attach remote end of the pipe to the peer socket. Note that peer's
            //  seqnum was incremented in find_endpoint function. We don't need it
            //  increased here.
            send_bind (peer.socket, pipes [1], false);

            // Save last endpoint URI
            options.last_endpoint = addr_;

            return true;
        }

        //  Choose the I/O thread to run the session in.
        IOThread io_thread = choose_io_thread (options.affinity);
        if (io_thread == null) {
            throw new ArgumentException("Empty IO Thread");
        }
        Address paddr = new Address (protocol, address);

        //  Resolve address (if needed by the protocol)
        if (protocol.Equals("tcp")) {
            paddr.resolved = ( new  TcpAddress () );
            paddr.resolved.resolve (
                address, options.ipv4only != 0 ? true : false);
        } else if(protocol.Equals("Ipc")) {
            paddr.resolved = ( new IpcAddress () );
            paddr.resolved.resolve (address, true);
        }
        //  Create session.
        SessionBase session = SessionBase.create (io_thread, true, this,
            options, paddr);
        Debug.Assert(session != null);

        //  PGM does not support subscription forwarding; ask for all data to be
        //  sent to this pipe.
        bool icanhasall = false;
        if (protocol.Equals("pgm") || protocol.Equals("epgm"))
            icanhasall = true;

        if (options.delay_attach_on_connect != 1 || icanhasall) {
            //  Create a bi-directional pipe.
            ZObject[] parents = {this, session};
            Pipe[] pipes = {null, null};
            int[] hwms = {options.sndhwm, options.rcvhwm};
            bool[] delays = {options.delay_on_disconnect, options.delay_on_close};
            Pipe.pipepair (parents, pipes, hwms, delays);

            //  Attach local end of the pipe to the socket object.
            attach_pipe (pipes [0], icanhasall);

            //  Attach remote end of the pipe to the session object later on.
            session.attach_pipe (pipes [1]);
        }
        
        // Save last endpoint URI
        options.last_endpoint = paddr.ToString ();

        add_endpoint (addr_, session);
        return true;
    }
    

    //  Creates new endpoint ID and adds the endpoint to the map.
    private void add_endpoint (String addr_, Own endpoint_)
    {
        //  Activate the session. Make it a child of this socket.
        launch_child (endpoint_);
        endpoints[addr_] =endpoint_;
    }
    
    public bool term_endpoint(String addr_) {

        if (ctx_terminated) {
            ZError.errno= (ZError.ETERM);
            return false;
        }
        
        //  Check whether endpoint address passed to the function is valid.
        if (addr_ == null) {
            throw new ArgumentException();
        }

        //  Process pending commands, if any, since there could be pending unprocessed process_own()'s
        //  (from launch_child() for example) we're asked to terminate now.
        bool rc = process_commands (0, false);
        if (!rc)
            return rc;

        if (!endpoints.ContainsKey(addr_)) {
            return false;
        }
        
        foreach (var e in endpoints)
	{
            term_child(e.Value);
	}

        endpoints.Clear();
        
        //  Find the endpoints range (if any) corresponding to the addr_ string.
        //Iterator<Entry<String, Own>> it = endpoints.entrySet().iterator();

        //while(it.hasNext()) {
        //    Entry<String, Own> e = it.next();
        //    term_child(e.getValue());
        //    it.remove();
        //}
        return true;

    }
    
    public bool send (Msg msg_, int flags_)
    {
        if (ctx_terminated) {
            ZError.errno = (ZError.ETERM);
            return false;
        }
        
        //  Check whether message passed to the function is valid.
        if (msg_ == null) {
            ZError.errno = (ZError.EFAULT);
            throw new ArgumentException();
        }

        //  Process pending commands, if any.
        bool rc = process_commands (0, true);
        if (!rc)
            return false;

        //  Clear any user-visible flags that are set on the message.
        msg_.reset_flags (Msg.more);

        //  At this point we impose the flags on the message.
        if ((flags_ & ZMQ.ZMQ_SNDMORE) > 0)
            msg_.SetFlags (Msg.more);

        //  Try to send the message.
        rc = xsend (msg_, flags_);
        if (rc)
            return true;
        if (!ZError.IsError(ZError.EAGAIN))
            return false;

        //  In case of non-blocking send we'll simply propagate
        //  the error - including EAGAIN - up the stack.
        if ((flags_ & ZMQ.ZMQ_DONTWAIT) > 0 || options.sndtimeo == 0)
            return false;

        //  Compute the time when the timeout should occur.
        //  If the timeout is infite, don't care. 
        int timeout = options.sndtimeo;
        long end = timeout < 0 ? 0 : (Clock.now_ms () + timeout);

        //  Oops, we couldn't send the message. Wait for the next
        //  command, process it and try to send the message again.
        //  If timeout is reached in the meantime, return EAGAIN.
        while (true) {
            if (!process_commands (timeout, false) )
                return false;
            
            rc = xsend (msg_, flags_);
            if (rc)
                break;
            
            if (!ZError.IsError(ZError.EAGAIN))
                return false;
            
            if (timeout > 0) {
                timeout = (int) (end - Clock.now_ms ());
                if (timeout <= 0) {
                    ZError.errno = (ZError.EAGAIN);
                    return false;
                }
            }
        }
        return true;
    }


    public Msg recv(int flags_) {
        
        if (ctx_terminated) {
            ZError.errno=(ZError.ETERM);
            return null;
        }
        
        //  Get the message.
        Msg msg_ = xrecv (flags_);
        if (msg_ == null && !ZError.IsError(ZError.EAGAIN))
            return null;

        //  Once every inbound_poll_rate messages check for signals and process
        //  incoming commands. This happens only if we are not polling altogether
        //  because there are messages available all the time. If poll occurs,
        //  ticks is set to zero and thus we avoid this code.
        //
        //  Note that 'recv' uses different command throttling algorithm (the one
        //  described above) from the one used by 'send'. This is because counting
        //  ticks is more efficient than doing RDTSC all the time.
        if (++ticks == Config.inbound_poll_rate) {
            if (!process_commands (0, false))
                return null;
            ticks = 0;
        }

        //  If we have the message, return immediately.
        if (msg_ != null) {
            extract_flags (msg_);
            return msg_;
        }

        //  If the message cannot be fetched immediately, there are two scenarios.
        //  For non-blocking recv, commands are processed in case there's an
        //  activate_reader command already waiting int a command pipe.
        //  If it's not, return EAGAIN.
        if ((flags_ & ZMQ.ZMQ_DONTWAIT) > 0 || options.rcvtimeo == 0) {
            if (!process_commands (0, false))
                return null;
            ticks = 0;

            msg_ = xrecv (flags_);
            if (msg_ == null)
                return null;
            extract_flags (msg_);
            return msg_;
        }

        //  Compute the time when the timeout should occur.
        //  If the timeout is infite, don't care. 
        int timeout = options.rcvtimeo;
        long end = timeout < 0 ? 0 : (Clock.now_ms () + timeout);

        //  In blocking scenario, commands are processed over and over again until
        //  we are able to fetch a message.
        bool block = (ticks != 0);
        while (true) {
            if (!process_commands (block ? timeout : 0, false))
                return null;
            msg_ = xrecv (flags_);
            if (msg_ != null) {
                ticks = 0;
                break;
            }
            if (!ZError.IsError(ZError.EAGAIN))
                return null;
            
            block = true;
            if (timeout > 0) {
                timeout = (int) (end - Clock.now_ms ());
                if (timeout <= 0) {
                    ZError.errno = (ZError.EAGAIN);
                    return null;
                }
            }
        }

        extract_flags (msg_);
        return msg_;

    }
    

    public void close() {
        //  Mark the socket as dead
        tag = 0xdeadbeef;
       
        //  Transfer the ownership of the socket from this application thread
        //  to the reaper thread which will take care of the rest of shutdown
        //  process.
        send_reap (this);

    }


    //  These functions are used by the polling mechanism to determine
    //  which events are to be reported from this socket.
    public bool has_in() {
        return xhas_in ();
    }
    
    public bool has_out() {
        return xhas_out ();
    }
    

    //  Using this function reaper thread ask the socket to register with
    //  its poller.
    public void start_reaping(Poller poller_) {
        
        //  Plug the socket to the reaper thread.
        poller = poller_;
        handle = mailbox.get_fd();
        poller.add_fd (handle, this);
        poller.set_pollin (handle);

        //  Initialise the termination and check whether it can be deallocated
        //  immediately.
        terminate ();
        check_destroy ();
    }
    
    //  Processes commands sent to this socket (if any). If timeout is -1,
    //  returns only after at least one command was processed.
    //  If throttle argument is true, commands are processed at most once
    //  in a predefined time period.
    private bool process_commands (int timeout_, bool throttle_)
    {
        Command cmd;
        bool ret = true;
        if (timeout_ != 0) {

            //  If we are asked to wait, simply ask mailbox to wait.
            cmd = mailbox.recv (timeout_);
        }
        else {

            //  If we are asked not to wait, check whether we haven't processed
            //  commands recently, so that we can throttle the new commands.

            //  Get the CPU's tick counter. If 0, the counter is not available.
            long tsc = 0 ; // save cpu Clock.rdtsc ();

            //  Optimised version of command processing - it doesn't have to check
            //  for incoming commands each time. It does so only if certain time
            //  elapsed since last command processing. Command delay varies
            //  depending on CPU speed: It's ~1ms on 3GHz CPU, ~2ms on 1.5GHz CPU
            //  etc. The optimisation makes sense only on platforms where getting
            //  a timestamp is a very cheap operation (tens of nanoseconds).
            if (tsc != 0 && throttle_) {

                //  Check whether TSC haven't jumped backwards (in case of migration
                //  between CPU cores) and whether certain time have elapsed since
                //  last command processing. If it didn't do nothing.
                if (tsc >= last_tsc && tsc - last_tsc <= Config.max_command_delay)
                    return true;
                last_tsc = tsc;
            }

            //  Check whether there are any commands pending for this thread.
            cmd = mailbox.recv (0);
        }

        
        //  Process all the commands available at the moment.
        while (true) {
            if (cmd == null)
                break;

            cmd.destination.process_command (cmd);
            cmd = mailbox.recv (0);
        }
        if (ctx_terminated) {
            ZError.errno = (ZError.ETERM);
            return false;
        }

        return ret;
    }

    protected override void process_stop ()
    {
        //  Here, someone have called zmq_term while the socket was still alive.
        //  We'll remember the fact so that any blocking call is interrupted and any
        //  further attempt to use the socket will return ETERM. The user is still
        //  responsible for calling zmq_close on the socket though!
        stop_monitor ();
        ctx_terminated = true;
        
    }
   
    protected override void process_bind (Pipe pipe_)
    {       
        attach_pipe (pipe_);
    }       

    protected override void process_term (int linger_)
    {
        //  Unregister all inproc endpoints associated with this socket.
        //  Doing this we make sure that no new pipes from other sockets (inproc)
        //  will be initiated.
        unregister_endpoints (this);

        //  Ask all attached pipes to terminate.
        for (int i = 0; i != pipes.Count; ++i)
            pipes[i].terminate (false);
        register_term_acks (pipes.Count);

        //  Continue the termination process immediately.
        base.process_term (linger_);
    }
    
    //  Delay actual destruction of the socket.
    protected override void process_destroy ()
    {
        destroyed = true;
    }
    
    //  The default implementation assumes there are no specific socket
    //  options for the particular socket type. If not so, overload this
    //  method.
    protected virtual bool xsetsockopt(int option_, Object optval_) {
        ZError.errno = (ZError.EINVAL);
        return false;
    }


    protected virtual bool xhas_out()
    {
        return false;
    }

    protected virtual bool xsend(Msg msg_, int flags_)
    {
        throw new NotSupportedException("Must Override");
    }

    protected virtual bool xhas_in() {
        return false;
    }
    

    protected virtual Msg xrecv(int flags_)
    {
        throw new NotSupportedException("Must Override");
    }

    protected virtual void xread_activated(Pipe pipe_)
    {
        throw new NotSupportedException("Must Override");
    }

    protected virtual void xwrite_activated(Pipe pipe_)
    {
        throw new NotSupportedException("Must Override");
    }

    protected virtual void xhiccuped(Pipe pipe_)
    {
        throw new NotSupportedException("Must override");
    }

    public virtual void in_event() {
        //  This function is invoked only once the socket is running in the context
        //  of the reaper thread. Process any commands from other threads/sockets
        //  that may be available at the moment. Ultimately, the socket will
        //  be destroyed.
        process_commands (0, false);
        check_destroy ();
    }

    public virtual void out_event()
    {
        throw new NotSupportedException();
    } 

    public virtual void timer_event(int id_)
    {
        throw new NotSupportedException();
    }


    //  To be called after processing commands or invoking any command
    //  handlers explicitly. If required, it will deallocate the socket.
    private void check_destroy ()
    {
        //  If the object was already marked as destroyed, finish the deallocation.
        if (destroyed) {

            //  Remove the socket from the reaper's poller.
            poller.rm_fd (handle);
            //  Remove the socket from the context.
            destroy_socket (this);

            //  Notify the reaper about the fact.
            send_reaped ();
            
            //  Deallocate.
            base.process_destroy ();
            
        }
    }

    public void read_activated (Pipe pipe_) 
    {
        xread_activated (pipe_);
    }

    public void write_activated (Pipe pipe_)
    {
        xwrite_activated (pipe_);
    }
    

    public void hiccuped (Pipe pipe_) 
    {
        if (options.delay_attach_on_connect == 1)
            pipe_.terminate (false);
        else
            // Notify derived sockets of the hiccup
            xhiccuped(pipe_);
    }
    

    public void terminated(Pipe pipe_) {
        //  Notify the specific socket type about the pipe termination.
        xterminated (pipe_);

        //  Remove the pipe from the list of attached pipes and confirm its
        //  termination if we are already shutting down.
        pipes.Remove(pipe_);
        if (is_terminating ())
            unregister_term_ack ();

    }
    
    

    //  Moves the flags from the message to local variables,
    //  to be later retrieved by getsockopt.
    private void extract_flags(Msg msg_) {
        //  Test whether IDENTITY flag is valid for this socket type.
        if ((msg_.flags & Msg.identity) > 0)
            Debug.Assert(options.recv_identity);

        //  Remove MORE flag.
        rcvmore = msg_.has_more();
    }


    public bool monitor (String addr_, int events_) {
        bool rc;
        if (ctx_terminated) {
            ZError.errno = (ZError.ETERM);
            return false;
        }

        // Support deregistering monitoring endpoints as well
        if (addr_ == null) {
            stop_monitor ();
            return true;
        }

        //  Parse addr_ string.
        Uri uri;
        try {
            uri = new Uri(addr_);
        }
        catch (UriFormatException ex)
        {
            ZError.errno  = (ZError.EINVAL);
            throw new ArgumentException (addr_,ex);
        }
        String protocol = uri.Scheme;
        String address = uri.Authority;
        String path = uri.AbsolutePath;
        if (address == null)
            address = path;

        check_protocol (protocol);

        // Event notification only supported over inproc://
        if (!protocol.Equals ("inproc")) {
            ZError.errno = (ZError.EPROTONOSUPPORT);
            return false;
        }

        // Register events to monitor
        monitor_events = events_;

        monitor_socket = get_ctx ().create_socket(ZMQ.ZMQ_PAIR);
        if (monitor_socket == null)
            return false;

        // Never block context termination on pending event messages
        int linger = 0;
        rc = monitor_socket.setsockopt (ZMQ.ZMQ_LINGER, linger);
        if (!rc)
             stop_monitor ();

        // Spawn the monitor socket endpoint
        rc = monitor_socket.bind (addr_);
        if (!rc)
             stop_monitor ();
        return rc;
    }
    
    public void event_connected (String addr, System.Net.Sockets.Socket ch) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_CONNECTED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_CONNECTED, addr, ch));
    }
    
    public void event_connect_delayed (String addr, int errno) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_CONNECT_DELAYED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_CONNECT_DELAYED, addr, errno));
    }
    
    public void event_connect_retried (String addr, int interval) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_CONNECT_RETRIED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_CONNECT_RETRIED, addr, interval));
    }
        
    public void event_listening (String addr, System.Net.Sockets.Socket ch) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_LISTENING) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_LISTENING, addr, ch));
    }
    
    public void event_bind_failed (String addr, int errno) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_BIND_FAILED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_BIND_FAILED, addr, errno));
    }
    
    public void event_accepted (String addr, System.Net.Sockets.Socket ch) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_ACCEPTED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_ACCEPTED, addr, ch));
    }
    
    public void event_accept_failed (String addr, int errno) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_ACCEPT_FAILED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_ACCEPT_FAILED, addr, errno));
    }
    
    public void event_closed (String addr, System.Net.Sockets.Socket ch) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_CLOSED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_CLOSED, addr, ch));
    }
    
    public void event_close_failed (String addr, int errno) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_CLOSE_FAILED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_CLOSE_FAILED, addr, errno));
    }
    
    public void event_disconnected (String addr, System.Net.Sockets.Socket ch) 
    {
        if ((monitor_events & ZMQ.ZMQ_EVENT_DISCONNECTED) == 0) 
            return;
        
        monitor_event (new ZMQ.Event (ZMQ.ZMQ_EVENT_DISCONNECTED, addr, ch));
    }
    
    protected void monitor_event (ZMQ.Event @event) 
    {
        
        if (monitor_socket == null)
            return;
        
        @event.write (monitor_socket);
    }
    
    protected void stop_monitor () 
    {

        if (monitor_socket != null) {
            monitor_socket.close();
            monitor_socket = null;
            monitor_events = 0;
        }
    }

    public override String ToString()
    {
        return base.ToString() + "[" + options.socket_id + "]";
    }

    public System.Net.Sockets.Socket get_fd() {
        return mailbox.get_fd();
    }

    public String typeString() {
        switch (options.type) {
        case ZMQ.ZMQ_PAIR:
            return "PAIR";
        case ZMQ.ZMQ_PUB:
            return "PUB";
        case ZMQ.ZMQ_SUB:
            return "SUB";
        case ZMQ.ZMQ_REQ:
            return "REQ";
        case ZMQ.ZMQ_REP:
            return "REP";
        case ZMQ.ZMQ_DEALER:
            return "DEALER";
        case ZMQ.ZMQ_ROUTER:
            return "ROUTER";
        case ZMQ.ZMQ_PULL:
            return "PULL";
        case ZMQ.ZMQ_PUSH:
            return "PUSH";
        default:
            return "UNKOWN";
        }
    }

}
