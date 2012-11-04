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

namespace zmq
{
	public abstract class SocketBase : Own, IPollEvents, Pipe.IPipeEvents
	{

		//private static Logger LOG = LoggerFactory.getLogger(SocketBase.class);

		private readonly Dictionary<String, Own> m_endpoints;

		//  Used to check whether the object is a socket.
		private uint m_tag;

		//  If true, associated context was already terminated.
		private bool m_ctxTerminated;

		//  If true, object should have been already destroyed. However,
		//  destruction is delayed while we unwind the stack to the point
		//  where it doesn't intersect the object being destroyed.
		private bool m_destroyed;

		//  Socket's mailbox object.
		private readonly Mailbox m_mailbox;

		//  List of attached pipes.
		//typedef array_t <pipe_t, 3> pipes_t;
		private readonly List<Pipe> m_pipes;

		//  Reaper's poller and handle of this socket within it.
		private Poller m_poller;
		private Socket m_handle;


		//  Timestamp of when commands were processed the last time.
		private long m_lastTsc;

		//  Number of messages received since last command processing.
		private int m_ticks;

		//  True if the last message received had MORE flag set.
		private bool m_rcvMore;

		// Monitor socket
		private SocketBase m_monitorSocket;

		// Bitmask of events being monitored
		private ZmqSocketEvent m_monitorEvents;

		protected SocketBase(Ctx parent, int tid, int sid)
			: base(parent, tid)
		{
			m_tag = 0xbaddecaf;
			m_ctxTerminated = false;
			m_destroyed = false;
			m_lastTsc = 0;
			m_ticks = 0;
			m_rcvMore = false;
			m_monitorSocket = null;
			m_monitorEvents = 0;

			m_options.SocketId = sid;

			m_endpoints = new Dictionary<string, Own>();
			m_pipes = new List<Pipe>();

			m_mailbox = new Mailbox("socket-" + sid);
		}

		//  Concrete algorithms for the x- methods are to be defined by
		//  individual socket types.
		abstract protected void XAttachPipe(Pipe pipe, bool icanhasall);
		abstract protected void XTerminated(Pipe pipe);


		//  Returns false if object is not a socket.
		public bool CheckTag()
		{
			return m_tag == 0xbaddecaf;
		}


		//  Create a socket of a specified type.
		public static SocketBase Create(ZmqSocketType type, Ctx parent,
																		int tid, int sid)
		{
			SocketBase s;
			switch (type)
			{

				case ZmqSocketType.ZMQ_PAIR:
					s = new Pair(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_PUB:
					s = new Pub(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_SUB:
					s = new Sub(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_REQ:
					s = new Req(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_REP:
					s = new Rep(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_DEALER:
					s = new Dealer(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_ROUTER:
					s = new Router(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_PULL:
					s = new Pull(parent, tid, sid);
					break;
				case ZmqSocketType.ZMQ_PUSH:
					s = new Push(parent, tid, sid);
					break;

				case ZmqSocketType.ZMQ_XPUB:
					s = new XPub(parent, tid, sid);
					break;

				case ZmqSocketType.ZMQ_XSUB:
					s = new XSub(parent, tid, sid);
					break;

				default:
					throw new ArgumentException("type=" + type);
			}
			return s;
		}

		public override void Destroy()
		{
			StopMonitor();

			Debug.Assert(m_destroyed);
		}

		//  Returns the mailbox associated with this socket.
		public Mailbox Mailbox
		{
			get { return m_mailbox; }
		}

		//  Interrupt blocking call if the socket is stuck in one.
		//  This function can be called from a different thread!
		public void Stop()
		{
			//  Called by ctx when it is terminated (zmq_term).
			//  'stop' command is sent from the threads that called zmq_term to
			//  the thread owning the socket. This way, blocking call in the
			//  owner thread can be interrupted.
			SendStop();

		}

		//  Check whether transport protocol, as specified in connect or
		//  bind, is available and compatible with the socket type.
		private void CheckProtocol(String protocol)
		{
			//  First check out whether the protcol is something we are aware of.
			if (!protocol.Equals("inproc") &&
					!protocol.Equals("ipc") && !protocol.Equals("tcp") /*&&
              !protocol_.equals("pgm") && !protocol_.equals("epgm")*/)
			{
				ZError.ErrorNumber = (ErrorNumber.EPROTONOSUPPORT);
				throw new NotSupportedException(protocol);
			}

			//  Check whether socket type and transport protocol match.
			//  Specifically, multicast protocols can't be combined with
			//  bi-directional messaging patterns (socket types).
			if ((protocol.Equals("pgm") || protocol.Equals("epgm")) &&
					m_options.SocketType != ZmqSocketType.ZMQ_PUB && m_options.SocketType != ZmqSocketType.ZMQ_SUB &&
					m_options.SocketType != ZmqSocketType.ZMQ_XPUB && m_options.SocketType != ZmqSocketType.ZMQ_XSUB)
			{
				ZError.ErrorNumber = (ErrorNumber.EPROTONOSUPPORT);
				throw new NotSupportedException(protocol + ",type=" + m_options.SocketType);
			}

			//  Protocol is available.
		}


		//  Register the pipe with this socket.
		private void AttachPipe(Pipe pipe)
		{
			AttachPipe(pipe, false);
		}

		private void AttachPipe(Pipe pipe, bool icanhasall)
		{
			//  First, register the pipe so that we can terminate it later on.

			pipe.set_event_sink(this);
			m_pipes.Add(pipe);

			//  Let the derived socket type know about new pipe.
			XAttachPipe(pipe, icanhasall);

			//  If the socket is already being closed, ask any new pipes to terminate
			//  straight away.
			if (IsTerminating)
			{
				RegisterTermAcks(1);
				pipe.terminate(false);
			}
		}

		public bool SetSocketOption(ZmqSocketOptions option, Object optval)
		{

			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = ErrorNumber.ETERM;
				return false;
			}

			//  First, check whether specific socket type overloads the option.
			bool rc = XSetSocketOption(option, optval);
			if (rc || !ZError.IsError(ErrorNumber.EINVAL))
				return false;

			//  If the socket type doesn't support the option, pass it to
			//  the generic option parser.
			ZError.Clear();
			return m_options.SetSocketOption(option, optval);
		}

		public int GetSocketOption(ZmqSocketOptions option)
		{

			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return -1;
			}

			if (option == ZmqSocketOptions.ZMQ_RCVMORE)
			{
				return m_rcvMore ? 1 : 0;
			}
			if (option == ZmqSocketOptions.ZMQ_EVENTS)
			{
				bool rc = ProcessCommands(0, false);
				if (!rc && (ZError.IsError(ErrorNumber.EINTR) || ZError.IsError(ErrorNumber.ETERM)))
					return -1;
				Debug.Assert(rc);
				int val = 0;
				if (HasOut())
					val |= ZMQ.ZmqPollout;
				if (HasIn())
					val |= ZMQ.ZmqPollin;
				return val;
			}

			return (int)GetSocketOptionX(option);
		}

		public Object GetSocketOptionX(ZmqSocketOptions option)
		{
			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return null;
			}

			if (option == ZmqSocketOptions.ZMQ_RCVMORE)
			{
				return m_rcvMore ? 1 : 0;
			}

			if (option == ZmqSocketOptions.ZMQ_FD)
			{
				return m_mailbox.FD;
			}

			if (option == ZmqSocketOptions.ZMQ_EVENTS)
			{
				bool rc = ProcessCommands(0, false);
				if (!rc && (ZError.IsError(ErrorNumber.EINTR) || ZError.IsError(ErrorNumber.ETERM)))
					return -1;
				Debug.Assert(rc);
				int val = 0;
				if (HasOut())
					val |= ZMQ.ZmqPollout;
				if (HasIn())
					val |= ZMQ.ZmqPollin;
				return val;
			}
			//  If the socket type doesn't support the option, pass it to
			//  the generic option parser.
			return m_options.GetSocketOption(option);

		}

		public bool Bind(String addr)
		{
			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return false;
			}

			//  Process pending commands, if any.
			bool brc = ProcessCommands(0, false);
			if (!brc)
				return false;

			//  Parse addr_ string.
			Uri uri;
			try
			{
				uri = new Uri(addr);
			}
			catch (Exception e)
			{
				throw new ArgumentException(addr, e);
			}
			String protocol = uri.Scheme;
			String address = uri.Authority;
			String path = uri.AbsolutePath;
			if (string.IsNullOrEmpty(address))
				address = path;

			CheckProtocol(protocol);

			if (protocol.Equals("inproc"))
			{
				Ctx.Endpoint endpoint = new Ctx.Endpoint(this, m_options);
				bool rc = RegisterEndpoint(addr, endpoint);
				if (rc)
				{
					// Save last endpoint URI
					m_options.LastEndpoint = addr;
				}
				return rc;
			}
			if (protocol.Equals("pgm") || protocol.Equals("epgm"))
			{
				//  For convenience's sake, bind can be used interchageable with
				//  connect for PGM and EPGM transports.
				return Connect(addr);
			}

			//  Remaining trasnports require to be run in an I/O thread, so at this
			//  point we'll choose one.
			IOThread ioThread = ChooseIOThread(m_options.Affinity);
			if (ioThread == null)
			{
				ZError.ErrorNumber = (ErrorNumber.EMTHREAD);
				return false;
			}

			if (protocol.Equals("tcp"))
			{
				TcpListener listener = new TcpListener(
					ioThread, this, m_options);
				bool rc = listener.SetAddress(address);
				if (!rc)
				{
					listener.Destroy();
					EventBindFailed(addr, ZError.ErrorNumber);
					//LOG.error("Failed to Bind", ZError.exc());
					return false;
				}

				// Save last endpoint URI
				m_options.LastEndpoint = listener.Address;

				AddEndpoint(addr, listener);
				return true;
			}

			if (protocol.Equals("ipc"))
			{
				IpcListener listener = new IpcListener(
					ioThread, this, m_options);
				bool rc = listener.SetAddress(address);
				if (!rc)
				{
					listener.Destroy();
					EventBindFailed(addr, ZError.ErrorNumber);
					return false;
				}

				// Save last endpoint URI
				m_options.LastEndpoint = listener.Address;

				AddEndpoint(addr, listener);
				return true;
			}

			Debug.Assert(false);
			return false;
		}

		public bool Connect(String addr)
		{
			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return false;
			}

			//  Process pending commands, if any.
			bool brc = ProcessCommands(0, false);
			if (!brc)
				return false;

			//  Parse addr_ string.
			Uri uri;
			try
			{
				uri = new Uri(addr);
			}
			catch (Exception e)
			{
				throw new ArgumentException(addr, e);
			}

			String protocol = uri.Scheme;
			String address = uri.Authority;
			String path = uri.AbsolutePath;
			if (string.IsNullOrEmpty(address))
				address = path;

			CheckProtocol(protocol);

			if (protocol.Equals("inproc"))
			{

				//  TODO: inproc connect is specific with respect to creating pipes
				//  as there's no 'reconnect' functionality implemented. Once that
				//  is in place we should follow generic pipe creation algorithm.

				//  Find the peer endpoint.
				Ctx.Endpoint peer = FindEndpoint(addr);
				if (peer.Socket == null)
					return false;
				// The total HWM for an inproc connection should be the sum of
				// the binder's HWM and the connector's HWM.
				int sndhwm;
				int rcvhwm;
				if (m_options.SendHighWatermark == 0 || peer.Options.ReceiveHighWatermark == 0)
					sndhwm = 0;
				else
					sndhwm = m_options.SendHighWatermark + peer.Options.ReceiveHighWatermark;
				if (m_options.ReceiveHighWatermark == 0 || peer.Options.SendHighWatermark == 0)
					rcvhwm = 0;
				else
					rcvhwm = m_options.ReceiveHighWatermark + peer.Options.SendHighWatermark;

				//  Create a bi-directional pipe to connect the peers.
				ZObject[] parents = { this, peer.Socket };
				Pipe[] pipes = { null, null };
				int[] hwms = { sndhwm, rcvhwm };
				bool[] delays = { m_options.DelayOnDisconnect, m_options.DelayOnClose };
				Pipe.pipepair(parents, pipes, hwms, delays);

				//  Attach local end of the pipe to this socket object.
				AttachPipe(pipes[0]);

				//  If required, send the identity of the peer to the local socket.
				if (peer.Options.RecvIdentity)
				{
					Msg id = new Msg(peer.Options.IdentitySize);
					id.Put(peer.Options.Identity, 0, peer.Options.IdentitySize);
					id.SetFlags(MsgFlags.Identity);
					bool written = pipes[0].write(id);
					Debug.Assert(written);
					pipes[0].flush();
				}

				//  If required, send the identity of the local socket to the peer.
				if (m_options.RecvIdentity)
				{
					Msg id = new Msg(m_options.IdentitySize);
					id.Put(m_options.Identity, 0, m_options.IdentitySize);
					id.SetFlags(MsgFlags.Identity);
					bool written = pipes[1].write(id);
					Debug.Assert(written);
					pipes[1].flush();
				}

				//  Attach remote end of the pipe to the peer socket. Note that peer's
				//  seqnum was incremented in find_endpoint function. We don't need it
				//  increased here.
				SendBind(peer.Socket, pipes[1], false);

				// Save last endpoint URI
				m_options.LastEndpoint = addr;

				return true;
			}

			//  Choose the I/O thread to run the session in.
			IOThread ioThread = ChooseIOThread(m_options.Affinity);
			if (ioThread == null)
			{
				throw new ArgumentException("Empty IO Thread");
			}
			Address paddr = new Address(protocol, address);

			//  Resolve address (if needed by the protocol)
			if (protocol.Equals("tcp"))
			{
				paddr.Resolved = (new TcpAddress());
				paddr.Resolved.Resolve(
					address, m_options.IPv4Only != 0);
			}
			else if (protocol.Equals("Ipc"))
			{
				paddr.Resolved = (new IpcAddress());
				paddr.Resolved.Resolve(address, true);
			}
			//  Create session.
			SessionBase session = SessionBase.Create(ioThread, true, this,
																								m_options, paddr);
			Debug.Assert(session != null);

			//  PGM does not support subscription forwarding; ask for all data to be
			//  sent to this pipe.
			bool icanhasall = false;
			if (protocol.Equals("pgm") || protocol.Equals("epgm"))
				icanhasall = true;

			if (m_options.DelayAttachOnConnect != 1 || icanhasall)
			{
				//  Create a bi-directional pipe.
				ZObject[] parents = { this, session };
				Pipe[] pipes = { null, null };
				int[] hwms = { m_options.SendHighWatermark, m_options.ReceiveHighWatermark };
				bool[] delays = { m_options.DelayOnDisconnect, m_options.DelayOnClose };
				Pipe.pipepair(parents, pipes, hwms, delays);

				//  Attach local end of the pipe to the socket object.
				AttachPipe(pipes[0], icanhasall);

				//  Attach remote end of the pipe to the session object later on.
				session.AttachPipe(pipes[1]);
			}

			// Save last endpoint URI
			m_options.LastEndpoint = paddr.ToString();

			AddEndpoint(addr, session);
			return true;
		}


		//  Creates new endpoint ID and adds the endpoint to the map.
		private void AddEndpoint(String addr, Own endpoint)
		{
			//  Activate the session. Make it a child of this socket.
			LaunchChild(endpoint);
			m_endpoints[addr] = endpoint;
		}

		public bool TermEndpoint(String addr)
		{

			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return false;
			}

			//  Check whether endpoint address passed to the function is valid.
			if (addr == null)
			{
				throw new ArgumentException();
			}

			//  Process pending commands, if any, since there could be pending unprocessed process_own()'s
			//  (from launch_child() for example) we're asked to terminate now.
			bool rc = ProcessCommands(0, false);
			if (!rc)
				return rc;

			if (!m_endpoints.ContainsKey(addr))
			{
				return false;
			}

			foreach (var e in m_endpoints)
			{
				TermChild(e.Value);
			}

			m_endpoints.Clear();

			//  Find the endpoints range (if any) corresponding to the addr_ string.
			//Iterator<Entry<String, Own>> it = endpoints.entrySet().iterator();

			//while(it.hasNext()) {
			//    Entry<String, Own> e = it.next();
			//    term_child(e.getValue());
			//    it.remove();
			//}
			return true;

		}

		public bool Send(Msg msg, ZmqSendRecieveOptions flags)
		{
			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return false;
			}

			//  Check whether message passed to the function is valid.
			if (msg == null)
			{
				ZError.ErrorNumber = (ErrorNumber.EFAULT);
				throw new ArgumentException();
			}

			//  Process pending commands, if any.
			bool rc = ProcessCommands(0, true);
			if (!rc)
				return false;

			//  Clear any user-visible flags that are set on the message.
			msg.ResetFlags(MsgFlags.More);

			//  At this point we impose the flags on the message.
			if ((flags & ZmqSendRecieveOptions.ZMQ_SNDMORE) > 0)
				msg.SetFlags(MsgFlags.More);

			//  Try to send the message.
			rc = XSend(msg, flags);
			if (rc)
				return true;
			if (!ZError.IsError(ErrorNumber.EAGAIN))
				return false;

			//  In case of non-blocking send we'll simply propagate
			//  the error - including EAGAIN - up the stack.
			if ((flags & ZmqSendRecieveOptions.ZMQ_DONTWAIT) > 0 || m_options.SendTimeout == 0)
				return false;

			//  Compute the time when the timeout should occur.
			//  If the timeout is infite, don't care. 
			int timeout = m_options.SendTimeout;
			long end = timeout < 0 ? 0 : (Clock.NowMs() + timeout);

			//  Oops, we couldn't send the message. Wait for the next
			//  command, process it and try to send the message again.
			//  If timeout is reached in the meantime, return EAGAIN.
			while (true)
			{
				if (!ProcessCommands(timeout, false))
					return false;

				rc = XSend(msg, flags);
				if (rc)
					break;

				if (!ZError.IsError(ErrorNumber.EAGAIN))
					return false;

				if (timeout > 0)
				{
					timeout = (int)(end - Clock.NowMs());
					if (timeout <= 0)
					{
						ZError.ErrorNumber = (ErrorNumber.EAGAIN);
						return false;
					}
				}
			}
			return true;
		}


		public Msg Recv(ZmqSendRecieveOptions flags)
		{

			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return null;
			}

			//  Get the message.
			Msg msg = XRecv(flags);
			if (msg == null && !ZError.IsError(ErrorNumber.EAGAIN))
				return null;

			//  Once every inbound_poll_rate messages check for signals and process
			//  incoming commands. This happens only if we are not polling altogether
			//  because there are messages available all the time. If poll occurs,
			//  ticks is set to zero and thus we avoid this code.
			//
			//  Note that 'recv' uses different command throttling algorithm (the one
			//  described above) from the one used by 'send'. This is because counting
			//  ticks is more efficient than doing RDTSC all the time.
			if (++m_ticks == Config.InboundPollRate)
			{
				if (!ProcessCommands(0, false))
					return null;
				m_ticks = 0;
			}

			//  If we have the message, return immediately.
			if (msg != null)
			{
				ExtractFlags(msg);
				return msg;
			}

			//  If the message cannot be fetched immediately, there are two scenarios.
			//  For non-blocking recv, commands are processed in case there's an
			//  activate_reader command already waiting int a command pipe.
			//  If it's not, return EAGAIN.
			if ((flags & ZmqSendRecieveOptions.ZMQ_DONTWAIT) > 0 || m_options.ReceiveTimeout == 0)
			{
				if (!ProcessCommands(0, false))
					return null;
				m_ticks = 0;

				msg = XRecv(flags);
				if (msg == null)
					return null;
				ExtractFlags(msg);
				return msg;
			}

			//  Compute the time when the timeout should occur.
			//  If the timeout is infite, don't care. 
			int timeout = m_options.ReceiveTimeout;
			long end = timeout < 0 ? 0 : (Clock.NowMs() + timeout);

			//  In blocking scenario, commands are processed over and over again until
			//  we are able to fetch a message.
			bool block = (m_ticks != 0);
			while (true)
			{
				if (!ProcessCommands(block ? timeout : 0, false))
					return null;
				msg = XRecv(flags);
				if (msg != null)
				{
					m_ticks = 0;
					break;
				}
				if (!ZError.IsError(ErrorNumber.EAGAIN))
					return null;

				block = true;
				if (timeout > 0)
				{
					timeout = (int)(end - Clock.NowMs());
					if (timeout <= 0)
					{
						ZError.ErrorNumber = (ErrorNumber.EAGAIN);
						return null;
					}
				}
			}

			ExtractFlags(msg);
			return msg;

		}


		public void Close()
		{
			//  Mark the socket as dead
			m_tag = 0xdeadbeef;

			//  Transfer the ownership of the socket from this application thread
			//  to the reaper thread which will take care of the rest of shutdown
			//  process.
			SendReap(this);

		}


		//  These functions are used by the polling mechanism to determine
		//  which events are to be reported from this socket.
		public bool HasIn()
		{
			return XHasIn();
		}

		public bool HasOut()
		{
			return XHasOut();
		}


		//  Using this function reaper thread ask the socket to register with
		//  its poller.
		public void StartReaping(Poller poller)
		{

			//  Plug the socket to the reaper thread.
			m_poller = poller;
			m_handle = m_mailbox.FD;
			m_poller.AddFD(m_handle, this);
			m_poller.SetPollin(m_handle);

			//  Initialise the termination and check whether it can be deallocated
			//  immediately.
			Terminate();
			CheckDestroy();
		}

		//  Processes commands sent to this socket (if any). If timeout is -1,
		//  returns only after at least one command was processed.
		//  If throttle argument is true, commands are processed at most once
		//  in a predefined time period.
		private bool ProcessCommands(int timeout, bool throttle)
		{
			Command cmd;
			bool ret = true;
			if (timeout != 0)
			{

				//  If we are asked to wait, simply ask mailbox to wait.
				cmd = m_mailbox.Recv(timeout);
			}
			else
			{

				//  If we are asked not to wait, check whether we haven't processed
				//  commands recently, so that we can throttle the new commands.

				//  Get the CPU's tick counter. If 0, the counter is not available.
				long tsc = 0; // save cpu Clock.rdtsc ();

				//  Optimised version of command processing - it doesn't have to check
				//  for incoming commands each time. It does so only if certain time
				//  elapsed since last command processing. Command delay varies
				//  depending on CPU speed: It's ~1ms on 3GHz CPU, ~2ms on 1.5GHz CPU
				//  etc. The optimisation makes sense only on platforms where getting
				//  a timestamp is a very cheap operation (tens of nanoseconds).
				if (tsc != 0 && throttle)
				{

					//  Check whether TSC haven't jumped backwards (in case of migration
					//  between CPU cores) and whether certain time have elapsed since
					//  last command processing. If it didn't do nothing.
					if (tsc >= m_lastTsc && tsc - m_lastTsc <= Config.MaxCommandDelay)
						return true;
					m_lastTsc = tsc;
				}

				//  Check whether there are any commands pending for this thread.
				cmd = m_mailbox.Recv(0);
			}


			//  Process all the commands available at the moment.
			while (true)
			{
				if (cmd == null)
					break;

				cmd.Destination.ProcessCommand(cmd);
				cmd = m_mailbox.Recv(0);
			}
			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return false;
			}

			return ret;
		}

		protected override void ProcessStop()
		{
			//  Here, someone have called zmq_term while the socket was still alive.
			//  We'll remember the fact so that any blocking call is interrupted and any
			//  further attempt to use the socket will return ETERM. The user is still
			//  responsible for calling zmq_close on the socket though!
			StopMonitor();
			m_ctxTerminated = true;

		}

		protected override void ProcessBind(Pipe pipe)
		{
			AttachPipe(pipe);
		}

		protected override void ProcessTerm(int linger)
		{
			//  Unregister all inproc endpoints associated with this socket.
			//  Doing this we make sure that no new pipes from other sockets (inproc)
			//  will be initiated.
			UnregisterEndpoints(this);

			//  Ask all attached pipes to terminate.
			for (int i = 0; i != m_pipes.Count; ++i)
				m_pipes[i].terminate(false);
			RegisterTermAcks(m_pipes.Count);

			//  Continue the termination process immediately.
			base.ProcessTerm(linger);
		}

		//  Delay actual destruction of the socket.
		protected override void ProcessDestroy()
		{
			m_destroyed = true;
		}

		//  The default implementation assumes there are no specific socket
		//  options for the particular socket type. If not so, overload this
		//  method.
		protected virtual bool XSetSocketOption(ZmqSocketOptions option, Object optval)
		{
			ZError.ErrorNumber = (ErrorNumber.EINVAL);
			return false;
		}


		protected virtual bool XHasOut()
		{
			return false;
		}

		protected virtual bool XSend(Msg msg, ZmqSendRecieveOptions flags)
		{
			throw new NotSupportedException("Must Override");
		}

		protected virtual bool XHasIn()
		{
			return false;
		}


		protected virtual Msg XRecv(ZmqSendRecieveOptions flags)
		{
			throw new NotSupportedException("Must Override");
		}

		protected virtual void XReadActivated(Pipe pipe)
		{
			throw new NotSupportedException("Must Override");
		}

		protected virtual void XWriteActivated(Pipe pipe)
		{
			throw new NotSupportedException("Must Override");
		}

		protected virtual void XHiccuped(Pipe pipe)
		{
			throw new NotSupportedException("Must override");
		}

		public virtual void InEvent()
		{
			//  This function is invoked only once the socket is running in the context
			//  of the reaper thread. Process any commands from other threads/sockets
			//  that may be available at the moment. Ultimately, the socket will
			//  be destroyed.
			ProcessCommands(0, false);
			CheckDestroy();
		}

		public virtual void OutEvent()
		{
			throw new NotSupportedException();
		}

		public virtual void TimerEvent(int id)
		{
			throw new NotSupportedException();
		}


		//  To be called after processing commands or invoking any command
		//  handlers explicitly. If required, it will deallocate the socket.
		private void CheckDestroy()
		{
			//  If the object was already marked as destroyed, finish the deallocation.
			if (m_destroyed)
			{

				//  Remove the socket from the reaper's poller.
				m_poller.RemoveFD(m_handle);
				//  Remove the socket from the context.
				DestroySocket(this);

				//  Notify the reaper about the fact.
				SendReaped();

				//  Deallocate.
				base.ProcessDestroy();

			}
		}

		public void ReadActivated(Pipe pipe)
		{
			XReadActivated(pipe);
		}

		public void WriteActivated(Pipe pipe)
		{
			XWriteActivated(pipe);
		}


		public void Hiccuped(Pipe pipe)
		{
			if (m_options.DelayAttachOnConnect == 1)
				pipe.terminate(false);
			else
				// Notify derived sockets of the hiccup
				XHiccuped(pipe);
		}


		public void Terminated(Pipe pipe)
		{
			//  Notify the specific socket type about the pipe termination.
			XTerminated(pipe);

			//  Remove the pipe from the list of attached pipes and confirm its
			//  termination if we are already shutting down.
			m_pipes.Remove(pipe);
			if (IsTerminating)
				UnregisterTermAck();

		}



		//  Moves the flags from the message to local variables,
		//  to be later retrieved by getsockopt.
		private void ExtractFlags(Msg msg)
		{
			//  Test whether IDENTITY flag is valid for this socket type.
			if ((msg.Flags & MsgFlags.Identity) != 0)
				Debug.Assert(m_options.RecvIdentity);

			//  Remove MORE flag.
			m_rcvMore = msg.HasMore;
		}


		public bool Monitor(String addr, ZmqSocketEvent events)
		{
			bool rc;
			if (m_ctxTerminated)
			{
				ZError.ErrorNumber = (ErrorNumber.ETERM);
				return false;
			}

			// Support deregistering monitoring endpoints as well
			if (addr == null)
			{
				StopMonitor();
				return true;
			}

			//  Parse addr_ string.
			Uri uri;
			try
			{
				uri = new Uri(addr);
			}
			catch (UriFormatException ex)
			{
				ZError.ErrorNumber = (ErrorNumber.EINVAL);
				throw new ArgumentException(addr, ex);
			}
			String protocol = uri.Scheme;
			String address = uri.Authority;
			String path = uri.AbsolutePath;
			if (string.IsNullOrEmpty(address))
				address = path;

			CheckProtocol(protocol);

			// Event notification only supported over inproc://
			if (!protocol.Equals("inproc"))
			{
				ZError.ErrorNumber = (ErrorNumber.EPROTONOSUPPORT);
				return false;
			}

			// Register events to monitor
			m_monitorEvents = events;

			m_monitorSocket = Ctx.CreateSocket(ZmqSocketType.ZMQ_PAIR);
			if (m_monitorSocket == null)
				return false;

			// Never block context termination on pending event messages
			int linger = 0;
			rc = m_monitorSocket.SetSocketOption(ZmqSocketOptions.ZMQ_LINGER, linger);
			if (!rc)
				StopMonitor();

			// Spawn the monitor socket endpoint
			rc = m_monitorSocket.Bind(addr);
			if (!rc)
				StopMonitor();
			return rc;
		}

		public void EventConnected(String addr, Socket ch)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_CONNECTED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_CONNECTED, addr, ch));
		}

		public void EventConnectDelayed(String addr, ErrorNumber errno)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_CONNECT_DELAYED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_CONNECT_DELAYED, addr, errno));
		}

		public void EventConnectRetried(String addr, int interval)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_CONNECT_RETRIED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_CONNECT_RETRIED, addr, interval));
		}

		public void EventListening(String addr, Socket ch)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_LISTENING) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_LISTENING, addr, ch));
		}

		public void EventBindFailed(String addr, ErrorNumber errno)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_BIND_FAILED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_BIND_FAILED, addr, errno));
		}

		public void EventAccepted(String addr, Socket ch)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_ACCEPTED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_ACCEPTED, addr, ch));
		}

		public void EventAcceptFailed(String addr, ErrorNumber errno)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_ACCEPT_FAILED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_ACCEPT_FAILED, addr, errno));
		}

		public void EventClosed(String addr, Socket ch)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_CLOSED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_CLOSED, addr, ch));
		}

		public void EventCloseFailed(String addr, ErrorNumber errno)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_CLOSE_FAILED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_CLOSE_FAILED, addr, errno));
		}

		public void EventDisconnected(String addr, Socket ch)
		{
			if ((m_monitorEvents & ZmqSocketEvent.ZMQ_EVENT_DISCONNECTED) == 0)
				return;

			MonitorEvent(new MonitorEvent(ZmqSocketEvent.ZMQ_EVENT_DISCONNECTED, addr, ch));
		}

		protected void MonitorEvent(MonitorEvent monitorEvent)
		{

			if (m_monitorSocket == null)
				return;

			monitorEvent.write(m_monitorSocket);
		}

		protected void StopMonitor()
		{

			if (m_monitorSocket != null)
			{
				m_monitorSocket.Close();
				m_monitorSocket = null;
				m_monitorEvents = 0;
			}
		}

		public override String ToString()
		{
			return base.ToString() + "[" + m_options.SocketId + "]";
		}

		public Socket FD
		{
			get { return m_mailbox.FD; }
		}

		public String GetTypeString()
		{
			switch (m_options.SocketType)
			{
				case ZmqSocketType.ZMQ_PAIR:
					return "PAIR";
				case ZmqSocketType.ZMQ_PUB:
					return "PUB";
				case ZmqSocketType.ZMQ_SUB:
					return "SUB";
				case ZmqSocketType.ZMQ_REQ:
					return "REQ";
				case ZmqSocketType.ZMQ_REP:
					return "REP";
				case ZmqSocketType.ZMQ_DEALER:
					return "DEALER";
				case ZmqSocketType.ZMQ_ROUTER:
					return "ROUTER";
				case ZmqSocketType.ZMQ_PULL:
					return "PULL";
				case ZmqSocketType.ZMQ_PUSH:
					return "PUSH";
				default:
					return "UNKOWN";
			}
		}

	}
}
