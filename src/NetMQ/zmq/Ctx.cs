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
using System.Linq;
using System.Threading;
using System.Diagnostics;

//Context object encapsulates all the global state associated with
//  the library.

namespace NetMQ.zmq
{
    public class Ctx
    {

        // private static Logger LOG = LoggerFactory.getLogger(Ctx.class);

        //  Information associated with inproc endpoint. Note that endpoint options
        //  are registered as well so that the peer can access them without a need
        //  for synchronisation, handshaking or similar.

        public class Endpoint
        {
            public Endpoint(SocketBase socket, Options options)
            {
                Socket = socket;
                Options = options;
            }

            public SocketBase Socket { get; private set; }
            public Options Options { get; private set; }
        }
        //  Used to check whether the object is a context.
        private uint m_tag;

        //  Sockets belonging to this context. We need the list so that
        //  we can notify the sockets when zmq_term() is called. The sockets
        //  will return ETERM then.
        private readonly List<SocketBase> m_sockets;

        //  List of unused thread slots.
        private readonly Stack<int> m_emptySlots;

        //  If true, zmq_init has been called but no socket has been created
        //  yet. Launching of I/O threads is delayed.
        private volatile bool m_starting;

        //  If true, zmq_term was already called.
        private bool m_terminating;

        //  Synchronisation of accesses to global slot-related data:
        //  sockets, empty_slots, terminating. It also synchronises
        //  access to zombie sockets as such (as opposed to slots) and provides
        //  a memory barrier to ensure that all CPU cores see the same data.

        private readonly object m_slotSync;

        //  The reaper thread.
        private Reaper m_reaper;

        //  I/O threads.
        private readonly List<IOThread> m_ioThreads;

        //  Array of pointers to mailboxes for both application and I/O threads.
        private int m_slotCount;
        private Mailbox[] m_slots;

        //  Mailbox for zmq_term thread.
        private readonly Mailbox m_termMailbox;

        //  List of inproc endpoints within this context.
        private readonly Dictionary<String, Endpoint> m_endpoints;

        //  Synchronisation of access to the list of inproc endpoints.		
        private readonly object m_endpointsSync;

        //  Maximum socket ID.
        private static int s_maxSocketId;

        //  Maximum number of sockets that can be opened at the same time.
        private int m_maxSockets;

        //  Number of I/O threads to launch.
        private int m_ioThreadCount;

        //  Synchronisation of access to context options.		
        private readonly object m_optSync;

        public const int TermTid = 0;
        public const int ReaperTid = 1;

        public Ctx()
        {
            m_tag = 0xabadcafe;
            m_starting = true;
            m_terminating = false;
            m_reaper = null;
            m_slotCount = 0;
            m_slots = null;
            m_maxSockets = ZMQ.ZmqMaxSocketsDflt;
            m_ioThreadCount = ZMQ.ZmqIOThreadsDflt;

            m_slotSync = new object();
            m_endpointsSync = new object();
            m_optSync = new object();

            m_termMailbox = new Mailbox("terminater");

            m_emptySlots = new Stack<int>();
            m_ioThreads = new List<IOThread>();
            m_sockets = new List<SocketBase>();
            m_endpoints = new Dictionary<String, Endpoint>();
        }

        protected void Destroy()
        {
            foreach (IOThread it in m_ioThreads)
            {
                it.Stop();
            }

            foreach (IOThread it in m_ioThreads)
            {
                it.Destroy();
            }

            if (m_reaper != null)
                m_reaper.Destroy();
            m_termMailbox.Close();

            m_tag = 0xdeadbeef;
        }

        //  Returns false if object is not a context.
        public bool CheckTag()
        {
            return m_tag == 0xabadcafe;
        }

        //  This function is called when user invokes zmq_term. If there are
        //  no more sockets open it'll cause all the infrastructure to be shut
        //  down. If there are open sockets still, the deallocation happens
        //  after the last one is closed.

        public void Terminate()
        {

            m_tag = 0xdeadbeef;

            Monitor.Enter(m_slotSync);

            if (!m_starting)
            {

                //  Check whether termination was already underway, but interrupted and now
                //  restarted.
                bool restarted = m_terminating;
                m_terminating = true;
                Monitor.Exit(m_slotSync);


                //  First attempt to terminate the context.
                if (!restarted)
                {

                    //  First send stop command to sockets so that any blocking calls
                    //  can be interrupted. If there are no sockets we can ask reaper
                    //  thread to stop.
                    Monitor.Enter(m_slotSync);
                    try
                    {
                        for (int i = 0; i != m_sockets.Count; i++)
                        {
                            m_sockets[i].Stop();
                        }
                        if (m_sockets.Count == 0)
                            m_reaper.Stop();
                    }
                    finally
                    {
                        Monitor.Exit(m_slotSync);
                    }
                }

                //  Wait till reaper thread closes all the sockets.
                Command cmd;

                try
                {
                    cmd = m_termMailbox.Recv(-1);
                }
                catch (NetMQException ex)
                {
                    if (ex.ErrorCode == ErrorCode.EINTR)
                    {
                        return;
                    }
                    else
                    {
                        throw;
                    }
                }

                Debug.Assert(cmd.CommandType == CommandType.Done);
                Monitor.Enter(m_slotSync);
                Debug.Assert(m_sockets.Count == 0);
            }
            Monitor.Exit(m_slotSync);

            //  Deallocate the resources.
            Destroy();

        }

        public void Set(ContextOption option, int optval)
        {
            if (option == ContextOption.MaxSockets && optval >= 1)
            {
                lock (m_optSync)
                {
                    m_maxSockets = optval;
                }
            }
            else
                if (option == ContextOption.IOThreads && optval >= 0)
                {
                    lock (m_optSync)
                    {
                        m_ioThreadCount = optval;
                    }
                }
                else
                {
                    throw InvalidException.Create("option = " + option);
                }
        }

        public int Get(ContextOption option)
        {
            int rc = 0;
            if (option == ContextOption.MaxSockets)
                rc = m_maxSockets;
            else
                if (option == ContextOption.IOThreads)
                    rc = m_ioThreadCount;
                else
                {
                    throw InvalidException.Create("option = " + option);
                }
            return rc;
        }

        public SocketBase CreateSocket(ZmqSocketType type)
        {
            SocketBase s = null;
            lock (m_slotSync)
            {
                if (m_starting)
                {

                    m_starting = false;
                    //  Initialise the array of mailboxes. Additional three slots are for
                    //  zmq_term thread and reaper thread.

                    int ios;
                    int mazmq;

                    lock (m_optSync)
                    {
                        mazmq = m_maxSockets;
                        ios = m_ioThreadCount;
                    }
                    m_slotCount = mazmq + ios + 2;
                    m_slots = new Mailbox[m_slotCount];
                    //alloc_Debug.Assert(slots);

                    //  Initialise the infrastructure for zmq_term thread.
                    m_slots[TermTid] = m_termMailbox;

                    //  Create the reaper thread.
                    m_reaper = new Reaper(this, ReaperTid);
                    //alloc_Debug.Assert(reaper);
                    m_slots[ReaperTid] = m_reaper.Mailbox;
                    m_reaper.Start();

                    //  Create I/O thread objects and launch them.
                    for (int i = 2; i != ios + 2; i++)
                    {
                        IOThread ioThread = new IOThread(this, i);
                        //alloc_Debug.Assert(io_thread);
                        m_ioThreads.Add(ioThread);
                        m_slots[i] = ioThread.Mailbox;
                        ioThread.Start();
                    }

                    //  In the unused part of the slot array, create a list of empty slots.
                    for (int i = (int)m_slotCount - 1;
                             i >= (int)ios + 2; i--)
                    {
                        m_emptySlots.Push(i);
                        m_slots[i] = null;
                    }

                }

                //  Once zmq_term() was called, we can't create new sockets.
                if (m_terminating)
                {
                    throw TerminatingException.Create();
                }

                //  If max_sockets limit was reached, return error.
                if (m_emptySlots.Count == 0)
                {
                    throw NetMQException.Create(ErrorCode.EMFILE);
                }

                //  Choose a slot for the socket.
                int slot = m_emptySlots.Pop();

                //  Generate new unique socket ID.
                int sid = Interlocked.Increment(ref s_maxSocketId);

                //  Create the socket and register its mailbox.
                s = SocketBase.Create(type, this, slot, sid);
                if (s == null)
                {
                    m_emptySlots.Push(slot);
                    return null;
                }
                m_sockets.Add(s);
                m_slots[slot] = s.Mailbox;

                //LOG.debug("NEW Slot [" + slot + "] " + s);
            }

            return s;
        }


        public void DestroySocket(SocketBase socket)
        {
            int tid;
            //  Free the associated thread slot.
            lock (m_slotSync)
            {
                tid = socket.ThreadId;
                m_emptySlots.Push(tid);
                m_slots[tid].Close();
                m_slots[tid] = null;

                //  Remove the socket from the list of sockets.
                m_sockets.Remove(socket);

                //  If zmq_term() was already called and there are no more socket
                //  we can ask reaper thread to terminate.
                if (m_terminating && m_sockets.Count == 0)
                    m_reaper.Stop();
            }

            //LOG.debug("Released Slot [" + socket_ + "] ");
        }

        //  Returns reaper thread object.
        public ZObject GetReaper()
        {
            return m_reaper;
        }

        //  Send command to the destination thread.
        public void SendCommand(int tid, Command command)
        {
            m_slots[tid].Send(command);
        }

        //  Returns the I/O thread that is the least busy at the moment.
        //  Affinity specifies which I/O threads are eligible (0 = all).
        //  Returns NULL if no I/O thread is available.
        public IOThread ChooseIOThread(long affinity)
        {
            if (m_ioThreads.Count == 0)
                return null;

            //  Find the I/O thread with minimum load.
            int minLoad = -1;
            IOThread selectedIOThread = null;

            for (int i = 0; i != m_ioThreads.Count; i++)
            {
                if (affinity == 0 || (affinity & (1L << i)) > 0)
                {
                    int load = m_ioThreads[i].Load;
                    if (selectedIOThread == null || load < minLoad)
                    {
                        minLoad = load;
                        selectedIOThread = m_ioThreads[i];
                    }
                }
            }
            return selectedIOThread;
        }

        //  Management of inproc endpoints.
        public void RegisterEndpoint(String addr, Endpoint endpoint)
        {
            bool exist;

            lock (m_endpointsSync)
            {
                exist = m_endpoints.ContainsKey(addr);

                m_endpoints[addr] = endpoint;
            }

            if (exist)
            {
                throw NetMQException.Create(ErrorCode.EADDRINUSE);
            }
        }

        public void UnregisterEndpoint(string addr, SocketBase socket)
        {
            lock (m_endpointsSync)
            {
                Endpoint endpoint;

                if (m_endpoints.TryGetValue(addr, out endpoint))
                {
                    if (socket != endpoint.Socket)
                    {
                        throw NetMQException.Create(ErrorCode.ENOENT);    
                    }

                    m_endpoints.Remove(addr);
                }
                else
                {
                    throw NetMQException.Create(ErrorCode.ENOENT);    
                }
            }
        }

        public void UnregisterEndpoints(SocketBase socket)
        {
            lock (m_endpointsSync)
            {

                IList<string> removeList = (from e in m_endpoints where e.Value.Socket == socket select e.Key).ToList();

                foreach (var item in removeList)
                {
                    m_endpoints.Remove(item);
                }
            }
        }

        public Endpoint FindEndpoint(String addr)
        {
            Endpoint endpoint = null;
            lock (m_endpointsSync)
            {
                if (addr == null || !m_endpoints.ContainsKey(addr))
                {
                    throw NetMQException.Create(ErrorCode.EINVAL);
                }

                endpoint = m_endpoints[addr];
                if (endpoint == null)
                {
                    throw NetMQException.Create(ErrorCode.ECONNREFUSED);
                }

                //  Increment the command sequence number of the peer so that it won't
                //  get deallocated until "bind" command is issued by the caller.
                //  The subsequent 'bind' has to be called with inc_seqnum parameter
                //  set to false, so that the seqnum isn't incremented twice.
                endpoint.Socket.IncSeqnum();
            }
            return endpoint;
        }
    }
}
