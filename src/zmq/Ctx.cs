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
using Wintellect.PowerCollections;

//Context object encapsulates all the global state associated with
//  the library.

namespace zmq
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
		private readonly List<SocketBase> sockets;

		//  List of unused thread slots.
		private readonly Deque<int> empty_slots;

		//  If true, zmq_init has been called but no socket has been created
		//  yet. Launching of I/O threads is delayed.
		private volatile bool m_starting;

		//  If true, zmq_term was already called.
		private bool m_terminating;

		//  Synchronisation of accesses to global slot-related data:
		//  sockets, empty_slots, terminating. It also synchronises
		//  access to zombie sockets as such (as opposed to slots) and provides
		//  a memory barrier to ensure that all CPU cores see the same data.
		
		private readonly object slot_sync;

		//  The reaper thread.
		private Reaper m_reaper;

		//  I/O threads.
		private readonly List<IOThread> m_ioThreads;

		//  Array of pointers to mailboxes for both application and I/O threads.
		private int m_slotCount;
		private Mailbox[] m_slots;

		//  Mailbox for zmq_term thread.
		private readonly Mailbox term_mailbox;

		//  List of inproc endpoints within this context.
		private readonly Dictionary<String, Endpoint> endpoints;

		//  Synchronisation of access to the list of inproc endpoints.		
		private readonly object endpoints_sync;

		//  Maximum socket ID.
		private static readonly AtomicInteger s_maxSocketId = new AtomicInteger(0);

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

			slot_sync = new object();
			endpoints_sync = new object();
			m_optSync = new object();

			term_mailbox = new Mailbox("terminater");

			empty_slots = new Deque<int>();
			m_ioThreads = new List<IOThread>();
			sockets = new List<SocketBase>();
			endpoints = new Dictionary<String, Endpoint>();
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
			term_mailbox.Close();

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

			Monitor.Enter(slot_sync);
			if (!m_starting)
			{

				//  Check whether termination was already underway, but interrupted and now
				//  restarted.
				bool restarted = m_terminating;
				m_terminating = true;
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
							sockets[i].Stop();
						if (sockets.Count == 0)
							m_reaper.Stop();
					}
					finally
					{

					}
				}

				//  Wait till reaper thread closes all the sockets.
				Command cmd;
				cmd = term_mailbox.Recv(-1);
				if (cmd == null)
					//throw new InvalidOperationException();
					throw new ArgumentException();

				Debug.Assert(cmd.CommandType == CommandType.Done);
				Monitor.Enter(slot_sync);
				Debug.Assert(sockets.Count == 0);
			}
			Monitor.Exit(slot_sync);

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
					throw new ArgumentException("option = " + option);
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
					throw new ArgumentException("option = " + option);
				}
			return rc;
		}

		public SocketBase CreateSocket(ZmqSocketType type)
		{
			SocketBase s = null;
			lock (slot_sync)
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
					m_slots[TermTid] = term_mailbox;

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
						empty_slots.AddToBack(i);
						m_slots[i] = null;
					}

				}

				//  Once zmq_term() was called, we can't create new sockets.
				if (m_terminating)
				{
					ZError.ErrorNumber = ErrorNumber.ETERM;
					return null;
				}

				//  If max_sockets limit was reached, return error.
				if (empty_slots.Count == 0)
				{
					ZError.ErrorNumber = ErrorNumber.EMFILE;
					return null;
				}

				//  Choose a slot for the socket.
				int slot = empty_slots.RemoveFromBack();

				//  Generate new unique socket ID.
				int sid = s_maxSocketId.IncrementAndGet();

				//  Create the socket and register its mailbox.
				s = SocketBase.Create(type, this, slot, sid);
				if (s == null)
				{
					empty_slots.AddToBack(slot);
					return null;
				}
				sockets.Add(s);
				m_slots[slot] = s.Mailbox;

				//LOG.debug("NEW Slot [" + slot + "] " + s);
			}

			return s;
		}


		public void DestroySocket(SocketBase socket)
		{
			int tid;
			//  Free the associated thread slot.
			lock (slot_sync)
			{
				tid = socket.Tid;
				empty_slots.AddToBack(tid);
				m_slots[tid].Close();
				m_slots[tid] = null;

				//  Remove the socket from the list of sockets.
				sockets.Remove(socket);

				//  If zmq_term() was already called and there are no more socket
				//  we can ask reaper thread to terminate.
				if (m_terminating && sockets.Count == 0)
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
		public bool RegisterEndpoint(String addr, Endpoint endpoint)
		{
			Endpoint inserted = null;

			lock (endpoints_sync)
			{
				inserted = endpoints[addr] = endpoint;
			}
			if (inserted != null)
			{
				ZError.ErrorNumber = ErrorNumber.EADDRINUSE;
				return false;
			}
			return true;
		}

		public void UnregisterEndpoints(SocketBase socket)
		{
			lock (endpoints_sync)
			{

				IList<string> removeList = (from e in endpoints where e.Value.Socket == socket select e.Key).ToList();

				foreach (var item in removeList)
				{
					endpoints.Remove(item);
				}
			}
		}

		public Endpoint FindEndpoint(String addr)
		{
			Endpoint endpoint = null;
			lock (endpoints_sync)
			{
				endpoint = endpoints[addr];
				if (endpoint == null)
				{
					ZError.ErrorNumber = ErrorNumber.ECONNREFUSED;
					return new Endpoint(null, new Options());
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
