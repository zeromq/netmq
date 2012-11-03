/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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
using System.Threading;
using System.Diagnostics;
using Wintellect.PowerCollections;
using NetMQ;

//Context object encapsulates all the global state associated with
//  the library.

public class Ctx
{

    // private static Logger LOG = LoggerFactory.getLogger(Ctx.class);

    //  Information associated with inproc endpoint. Note that endpoint options
    //  are registered as well so that the peer can access them without a need
    //  for synchronisation, handshaking or similar.

    public class Endpoint
    {
        public SocketBase socket;
        public Options options;

        public Endpoint(SocketBase socket_, Options options_)
        {
            socket = socket_;
            options = options_;
        }

    }
    //  Used to check whether the object is a context.
    private uint tag;

    //  Sockets belonging to this context. We need the list so that
    //  we can notify the sockets when zmq_term() is called. The sockets
    //  will return ETERM then.
    private List<SocketBase> sockets;

    //  List of unused thread slots.
    private Deque<int> empty_slots;

    //  If true, zmq_init has been called but no socket has been created
    //  yet. Launching of I/O threads is delayed.
    private volatile bool starting;

    //  If true, zmq_term was already called.
    private bool terminating;

    //  Synchronisation of accesses to global slot-related data:
    //  sockets, empty_slots, terminating. It also synchronises
    //  access to zombie sockets as such (as opposed to slots) and provides
    //  a memory barrier to ensure that all CPU cores see the same data.

    //private Lock slot_sync;
    private object slot_sync;

    //  The reaper thread.
    private Reaper reaper;

    //  I/O threads.
    private List<IOThread> io_threads;

    //  Array of pointers to mailboxes for both application and I/O threads.
    private int slot_count;
    private Mailbox[] slots;

    //  Mailbox for zmq_term thread.
    private Mailbox term_mailbox;

    //  List of inproc endpoints within this context.
    private Dictionary<String, Endpoint> endpoints;

    //  Synchronisation of access to the list of inproc endpoints.
    //private Lock endpoints_sync;

    private object endpoints_sync;

    //  Maximum socket ID.
    private static AtomicInteger max_socket_id = new AtomicInteger(0);

    //  Maximum number of sockets that can be opened at the same time.
    private int max_sockets;

    //  Number of I/O threads to launch.
    private int io_thread_count;

    //  Synchronisation of access to context options.
    //private Lock opt_sync;
    private object opt_sync;

    public static int term_tid = 0;
    public static int reaper_tid = 1;

    public Ctx()
    {
        tag = 0xabadcafe;
        starting = true;
        terminating = false;
        reaper = null;
        slot_count = 0;
        slots = null;
        max_sockets = ZMQ.ZMQ_MAX_SOCKETS_DFLT;
        io_thread_count = ZMQ.ZMQ_IO_THREADS_DFLT;

        slot_sync = new object();
        endpoints_sync = new object();
        opt_sync = new object();

        term_mailbox = new Mailbox("terminater");

        empty_slots = new Deque<int>();
        io_threads = new List<IOThread>();
        sockets = new List<SocketBase>();
        endpoints = new Dictionary<String, Endpoint>();
    }

    protected void destroy()
    {
        foreach (IOThread it in io_threads)
        {
            it.stop();
        }

        foreach (IOThread it in io_threads)
        {
            it.destroy();
        }

        if (reaper != null)
            reaper.destroy();
        term_mailbox.close();

        tag = 0xdeadbeef;
    }

    //  Returns false if object is not a context.
    public bool check_tag()
    {
        return tag == 0xabadcafe;
    }

    //  This function is called when user invokes zmq_term. If there are
    //  no more sockets open it'll cause all the infrastructure to be shut
    //  down. If there are open sockets still, the deallocation happens
    //  after the last one is closed.

    public void terminate()
    {

        tag = 0xdeadbeef;

        Monitor.Enter(slot_sync);
        if (!starting)
        {

            //  Check whether termination was already underway, but interrupted and now
            //  restarted.
            bool restarted = terminating;
            terminating = true;
            Monitor.Exit(slot_sync);


            //  First attempt to terminate the context.
            if (!restarted)
            {

                //  First send stop command to sockets so that any blocking calls
                //  can be interrupted. If there are no sockets we can ask reaper
                //  thread to stop.
                Monitor.Enter(slot_sync);
                try
                {
                    for (int i = 0; i != sockets.Count; i++)
                        sockets[i].stop();
                    if (sockets.Count == 0)
                        reaper.stop();
                }
                finally
                {

                }
            }

            //  Wait till reaper thread closes all the sockets.
            Command cmd;
            cmd = term_mailbox.recv(-1);
            if (cmd == null)
                //throw new InvalidOperationException();
                throw new ArgumentException();

            Debug.Assert(cmd.type == Command.CommandType.done);
            Monitor.Enter(slot_sync);
            Debug.Assert(sockets.Count == 0);
        }
        Monitor.Exit(slot_sync);

        //  Deallocate the resources.
        destroy();

    }

    public void set(int option_, int optval_)
    {
        if (option_ == ZMQ.ZMQ_MAX_SOCKETS && optval_ >= 1)
        {
            lock (opt_sync)
            {
                max_sockets = optval_;
            }
        }
        else
            if (option_ == ZMQ.ZMQ_IO_THREADS && optval_ >= 0)
            {
                lock (opt_sync)
                {
                    io_thread_count = optval_;
                }
            }
            else
            {
                throw new ArgumentException("option = " + option_);
            }
    }

    public int get(int option_)
    {
        int rc = 0;
        if (option_ == ZMQ.ZMQ_MAX_SOCKETS)
            rc = max_sockets;
        else
            if (option_ == ZMQ.ZMQ_IO_THREADS)
                rc = io_thread_count;
            else
            {
                throw new ArgumentException("option = " + option_);
            }
        return rc;
    }

    public SocketBase create_socket(int type_)
    {
        SocketBase s = null;
        lock (slot_sync)
        {
            if (starting)
            {

                starting = false;
                //  Initialise the array of mailboxes. Additional three slots are for
                //  zmq_term thread and reaper thread.

                int ios;
                int mazmq;

                lock (opt_sync)
                {
                    mazmq = max_sockets;
                    ios = io_thread_count;
                }
                slot_count = mazmq + ios + 2;
                slots = new Mailbox[slot_count];
                //alloc_Debug.Assert(slots);

                //  Initialise the infrastructure for zmq_term thread.
                slots[term_tid] = term_mailbox;

                //  Create the reaper thread.
                reaper = new Reaper(this, reaper_tid);
                //alloc_Debug.Assert(reaper);
                slots[reaper_tid] = reaper.get_mailbox();
                reaper.start();

                //  Create I/O thread objects and launch them.
                for (int i = 2; i != ios + 2; i++)
                {
                    IOThread io_thread = new IOThread(this, i);
                    //alloc_Debug.Assert(io_thread);
                    io_threads.Add(io_thread);
                    slots[i] = io_thread.get_mailbox();
                    io_thread.start();
                }

                //  In the unused part of the slot array, create a list of empty slots.
                for (int i = (int)slot_count - 1;
                      i >= (int)ios + 2; i--)
                {
                    empty_slots.AddToBack(i);
                    slots[i] = null;
                }

            }

            //  Once zmq_term() was called, we can't create new sockets.
            if (terminating)
            {
                ZError.errno = ZError.ETERM;
                return null;
            }

            //  If max_sockets limit was reached, return error.
            if (empty_slots.Count == 0)
            {
                ZError.errno = ZError.EMFILE;
                return null;
            }

            //  Choose a slot for the socket.
            int slot = empty_slots.RemoveFromBack();

            //  Generate new unique socket ID.
            int sid = max_socket_id.incrementAndGet();

            //  Create the socket and register its mailbox.
            s = SocketBase.create(type_, this, slot, sid);
            if (s == null)
            {
                empty_slots.AddToBack(slot);
                return null;
            }
            sockets.Add(s);
            slots[slot] = s.get_mailbox();

            //LOG.debug("NEW Slot [" + slot + "] " + s);
        }

        return s;
    }


    public void destroy_socket(SocketBase socket_)
    {


        int tid;
        //  Free the associated thread slot.
        lock (slot_sync)
        {
            tid = socket_.tid;
            empty_slots.AddToBack(tid);
            slots[tid].close();
            slots[tid] = null;

            //  Remove the socket from the list of sockets.
            sockets.Remove(socket_);

            //  If zmq_term() was already called and there are no more socket
            //  we can ask reaper thread to terminate.
            if (terminating && sockets.Count == 0)
                reaper.stop();
        }

        //LOG.debug("Released Slot [" + socket_ + "] ");
    }

    //  Returns reaper thread object.
    public ZObject get_reaper()
    {
        return reaper;
    }

    //  Send command to the destination thread.
    public void send_command(int tid_, Command command_)
    {
        slots[tid_].send(command_);
    }

    //  Returns the I/O thread that is the least busy at the moment.
    //  Affinity specifies which I/O threads are eligible (0 = all).
    //  Returns NULL if no I/O thread is available.
    public IOThread choose_io_thread(long affinity_)
    {
        if (io_threads.Count == 0)
            return null;

        //  Find the I/O thread with minimum load.
        int min_load = -1;
        IOThread selected_io_thread = null;

        for (int i = 0; i != io_threads.Count; i++)
        {
            if (affinity_ == 0 || (affinity_ & (1L << i)) > 0)
            {
                int load = io_threads[i].get_load();
                if (selected_io_thread == null || load < min_load)
                {
                    min_load = load;
                    selected_io_thread = io_threads[i];
                }
            }
        }
        return selected_io_thread;
    }

    //  Management of inproc endpoints.
    public bool register_endpoint(String addr_, Endpoint endpoint_)
    {


        Endpoint inserted = null;

        lock (endpoints_sync)
        {
            inserted = endpoints[addr_] = endpoint_;
        }
        if (inserted != null)
        {
            ZError.errno = ZError.EADDRINUSE;
            return false;
        }
        return true;
    }

    public void unregister_endpoints(SocketBase socket_)
    {


        lock (endpoints_sync)
        {

            IList<string> removeList = new List<string>();

            foreach (var e in endpoints)
            {
                if (e.Value.socket == socket_)
                {
                    removeList.Add(e.Key);
                }
            }

            foreach (var item in removeList)
            {
                endpoints.Remove(item);
            }



        }
    }

    public Endpoint find_endpoint(String addr_)
    {
        Endpoint endpoint = null;
        lock (endpoints_sync)
        {
            endpoint = endpoints[addr_];
            if (endpoint == null)
            {
                ZError.errno = ZError.ECONNREFUSED;
                return new Endpoint(null, new Options());
            }

            //  Increment the command sequence number of the peer so that it won't
            //  get deallocated until "bind" command is issued by the caller.
            //  The subsequent 'bind' has to be called with inc_seqnum parameter
            //  set to false, so that the seqnum isn't incremented twice.
            endpoint.socket.inc_seqnum();
        }
        return endpoint;
    }
}
