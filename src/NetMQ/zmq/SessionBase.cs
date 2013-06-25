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

namespace NetMQ.zmq
{
	public class SessionBase : Own,
														 Pipe.IPipeEvents, IPollEvents,
														 IMsgSink, IMsgSource
	{
		//  If true, this session (re)connects to the peer. Otherwise, it's
		//  a transient session created by the listener.
		private readonly bool m_connect;

		//  Pipe connecting the session to its socket.
		private Pipe m_pipe;

		//  This set is added to with pipes we are disconnecting, but haven't yet completed
		private readonly HashSet<Pipe> m_terminatingPipes;

		//  This flag is true if the remainder of the message being processed
		//  is still in the in pipe.
		private bool m_incompleteIn;

		//  True if termination have been suspended to push the pending
		//  messages to the network.
		private bool m_pending;

		//  The protocol I/O engine connected to the session.
		private IEngine m_engine;

		//  The socket the session belongs to.
		private readonly SocketBase m_socket;

		//  I/O thread the session is living in. It will be used to plug in
		//  the engines into the same thread.
		private readonly IOThread m_ioThread;

		//  ID of the linger timer
		private const int LingerTimerId = 0x20;

		//  True is linger timer is running.
		private bool m_hasLingerTimer;

		//  If true, identity has been sent/received from the network.
		private bool m_identitySent;
		private bool m_identityReceived;

		//  Protocol and address to use when connecting.
		private readonly Address m_addr;

		private readonly IOObject m_ioObject;

		public static SessionBase Create(IOThread ioThread, bool connect,
																		 SocketBase socket, Options options, Address addr)
		{

			SessionBase s;
			switch (options.SocketType)
			{
				case ZmqSocketType.Req:
					s = new Req.ReqSession(ioThread, connect,
																	 socket, options, addr);
					break;
				case ZmqSocketType.Dealer:
					s = new Dealer.DealerSession(ioThread, connect,
																				socket, options, addr);
					break;
				case ZmqSocketType.Rep:
					s = new Rep.RepSession(ioThread, connect,
																	socket, options, addr);
					break;
				case ZmqSocketType.Router:
					s = new Router.RouterSession(ioThread, connect,
																				socket, options, addr);
					break;
				case ZmqSocketType.Pub:
					s = new Pub.PubSession(ioThread, connect,
																	socket, options, addr);
					break;
				case ZmqSocketType.Xpub:
					s = new XPub.XPubSession(ioThread, connect,
																	 socket, options, addr);
					break;
				case ZmqSocketType.Sub:
					s = new Sub.SubSession(ioThread, connect,
																	 socket, options, addr);
					break;
				case ZmqSocketType.Xsub:
					s = new XSub.XSubSession(ioThread, connect,
																		socket, options, addr);
					break;

				case ZmqSocketType.Push:
					s = new Push.PushSession(ioThread, connect,
																		socket, options, addr);
					break;
				case ZmqSocketType.Pull:
					s = new Pull.PullSession(ioThread, connect,
																		socket, options, addr);
					break;
				case ZmqSocketType.Pair:
					s = new Pair.PairSession(ioThread, connect,
																		socket, options, addr);
					break;
				default:
					throw InvalidException.Create("type=" + options.SocketType);					

			}
			return s;
		}

		public SessionBase(IOThread ioThread, bool connect,
											 SocketBase socket, Options options, Address addr)
			: base(ioThread, options)
		{
			m_ioObject = new IOObject(ioThread);

			m_connect = connect;
			m_pipe = null;
			m_incompleteIn = false;
			m_pending = false;
			m_engine = null;
			m_socket = socket;
			m_ioThread = ioThread;
			m_hasLingerTimer = false;
			m_identitySent = false;
			m_identityReceived = false;
			m_addr = addr;

			m_terminatingPipes = new HashSet<Pipe>();
		}

		public override void Destroy()
		{
			Debug.Assert(m_pipe == null);

			//  If there's still a pending linger timer, remove it.
			if (m_hasLingerTimer)
			{
				m_ioObject.CancelTimer(LingerTimerId);
				m_hasLingerTimer = false;
			}

			//  Close the engine.
			if (m_engine != null)
				m_engine.Terminate();

		}


		//  To be used once only, when creating the session.
		public void AttachPipe(Pipe pipe)
		{
			Debug.Assert(!IsTerminating);
			Debug.Assert(m_pipe == null);
			Debug.Assert(pipe != null);
			m_pipe = pipe;
			m_pipe.SetEventSink(this);
		}

		public virtual Msg PullMsg()
		{

			Msg msg;

			//  First message to send is identity
			if (!m_identitySent)
			{
				msg = new Msg(m_options.IdentitySize);
				msg.Put(m_options.Identity, 0, m_options.IdentitySize);
				m_identitySent = true;
				m_incompleteIn = false;

				return msg;
			}

			if (m_pipe == null || (msg = m_pipe.Read()) == null)
			{
				return null;
			}
			m_incompleteIn = msg.HasMore;

			return msg;

		}

		public virtual void PushMsg(Msg msg)
		{
			//  First message to receive is identity (if required).
			if (!m_identityReceived)
			{
				msg.SetFlags(MsgFlags.Identity);
				m_identityReceived = true;

				if (!m_options.RecvIdentity)
				{
					return;
				}
			}

			if (m_pipe != null && m_pipe.Write(msg))
			{
				return;
			}

			throw AgainException.Create();
		}


		protected virtual void Reset()
		{
			//  Restore identity flags.
			m_identitySent = false;
			m_identityReceived = false;
		}


		public void Flush()
		{
			if (m_pipe != null)
				m_pipe.Flush();
		}


		//  Remove any half processed messages. Flush unflushed messages.
		//  Call this function when engine disconnect to get rid of leftovers.
		private void CleanPipes()
		{
			if (m_pipe != null)
			{

				//  Get rid of half-processed messages in the out pipe. Flush any
				//  unflushed messages upstream.
				m_pipe.Rollback();
				m_pipe.Flush();

				//  Remove any half-read message from the in pipe.
				while (m_incompleteIn)
				{
					Msg msg = PullMsg();
					if (msg == null)
					{
						Debug.Assert(!m_incompleteIn);
						break;
					}
					msg.Close();
				}
			}
		}

		public void Terminated(Pipe pipe)
		{
			//  Drop the reference to the deallocated pipe.
			Debug.Assert(m_pipe == pipe || m_terminatingPipes.Contains(pipe));

			if (m_pipe == pipe)
				// If this is our current pipe, remove it
				m_pipe = null;
			else
				// Remove the pipe from the detached pipes set
				m_terminatingPipes.Remove(pipe);

			//  If we are waiting for pending messages to be sent, at this point
			//  we are sure that there will be no more messages and we can proceed
			//  with termination safely.
			if (m_pending && m_pipe == null && m_terminatingPipes.Count == 0)
				ProceedWithTerm();
		}

		public void ReadActivated(Pipe pipe)
		{
			// Skip activating if we're detaching this pipe
			if (m_pipe != pipe)
			{
				Debug.Assert(m_terminatingPipes.Contains(pipe));
				return;
			}

			if (m_engine != null)
				m_engine.ActivateOut();
			else
				m_pipe.CheckRead();
		}

		public void WriteActivated(Pipe pipe)
		{
			// Skip activating if we're detaching this pipe
			if (m_pipe != pipe)
			{
				Debug.Assert(m_terminatingPipes.Contains(pipe));
				return;
			}


			if (m_engine != null)
				m_engine.ActivateIn();
		}

		public void Hiccuped(Pipe pipe)
		{
			//  Hiccups are always sent from session to socket, not the other
			//  way round.
			throw new NotSupportedException("Must Override");

		}

		public SocketBase Socket
		{
			get { return m_socket; }
		}

		protected override void ProcessPlug()
		{
			m_ioObject.SetHandler(this);
			if (m_connect)
				StartConnecting(false);
		}


		protected override void ProcessAttach(IEngine engine)
		{
			Debug.Assert(engine != null);

			//  Create the pipe if it does not exist yet.
			if (m_pipe == null && !IsTerminating)
			{
				ZObject[] parents = { this, m_socket };
				int[] hwms = { m_options.ReceiveHighWatermark, m_options.SendHighWatermark };
				bool[] delays = { m_options.DelayOnClose, m_options.DelayOnDisconnect };
				Pipe[] pipes = Pipe.PipePair(parents, hwms, delays);

				//  Plug the local end of the pipe.
				pipes[0].SetEventSink(this);

				//  Remember the local end of the pipe.
				Debug.Assert(m_pipe == null);
				m_pipe = pipes[0];

				//  Ask socket to plug into the remote end of the pipe.
				SendBind(m_socket, pipes[1]);
			}

			//  Plug in the engine.
			Debug.Assert(m_engine == null);
			m_engine = engine;
			m_engine.Plug(m_ioThread, this);
		}

		public void Detach()
		{
			//  Engine is dead. Let's forget about it.
			m_engine = null;

			//  Remove any half-done messages from the pipes.
			CleanPipes();

			//  Send the event to the derived class.
			Detached();

			//  Just in case there's only a delimiter in the pipe.
			if (m_pipe != null)
				m_pipe.CheckRead();
		}

		protected override void ProcessTerm(int linger)
		{
			Debug.Assert(!m_pending);

			//  If the termination of the pipe happens before the term command is
			//  delivered there's nothing much to do. We can proceed with the
			//  stadard termination immediately.
			if (m_pipe == null)
			{
				ProceedWithTerm();
				return;
			}

			m_pending = true;

			//  If there's finite linger value, delay the termination.
			//  If linger is infinite (negative) we don't even have to set
			//  the timer.
			if (linger > 0)
			{
				Debug.Assert(!m_hasLingerTimer);
				m_ioObject.AddTimer(linger, LingerTimerId);
				m_hasLingerTimer = true;
			}

			//  Start pipe termination process. Delay the termination till all messages
			//  are processed in case the linger time is non-zero.
			m_pipe.Terminate(linger != 0);

			//  TODO: Should this go into pipe_t::terminate ?
			//  In case there's no engine and there's only delimiter in the
			//  pipe it wouldn't be ever read. Thus we check for it explicitly.
			m_pipe.CheckRead();
		}


		//  Call this function to move on with the delayed process_term.
		private void ProceedWithTerm()
		{
			//  The pending phase have just ended.
			m_pending = false;

			//  Continue with standard termination.
			base.ProcessTerm(0);
		}


		public void TimerEvent(int id)
		{

			//  Linger period expired. We can proceed with termination even though
			//  there are still pending messages to be sent.
			Debug.Assert(id == LingerTimerId);
			m_hasLingerTimer = false;

			//  Ask pipe to terminate even though there may be pending messages in it.
			Debug.Assert(m_pipe != null);
			m_pipe.Terminate(false);
		}


		private void Detached()
		{
			//  Transient session self-destructs after peer disconnects.
			if (!m_connect)
			{
				Terminate();
				return;
			}

			//  For delayed connect situations, terminate the pipe
			//  and reestablish later on
			if (m_pipe != null && m_options.DelayAttachOnConnect
					&& m_addr.Protocol != "pgm" && m_addr.Protocol != "epgm")
			{
				m_pipe.Hiccup();
				m_pipe.Terminate(false);
				m_terminatingPipes.Add(m_pipe);
				m_pipe = null;
			}

			Reset();

			//  Reconnect.
			if (m_options.ReconnectIvl != -1)
				StartConnecting(true);

			//  For subscriber sockets we hiccup the inbound pipe, which will cause
			//  the socket object to resend all the subscriptions.
			if (m_pipe != null && (m_options.SocketType == ZmqSocketType.Sub || m_options.SocketType == ZmqSocketType.Xsub))
				m_pipe.Hiccup();

		}


		private void StartConnecting(bool wait)
		{
			Debug.Assert(m_connect);

			//  Choose I/O thread to run connecter in. Given that we are already
			//  running in an I/O thread, there must be at least one available.
			IOThread ioThread = ChooseIOThread(m_options.Affinity);
			Debug.Assert(ioThread != null);

			//  Create the connecter object.

			if (m_addr.Protocol.Equals("tcp"))
			{
				TcpConnecter connecter = new TcpConnecter(
					ioThread, this, m_options, m_addr, wait);
				//alloc_Debug.Assert(connecter);
				LaunchChild(connecter);
				return;
			}

			if (m_addr.Protocol.Equals("ipc"))
			{
				IpcConnecter connecter = new IpcConnecter(
					ioThread, this, m_options, m_addr, wait);
				//alloc_Debug.Assert(connecter);
				LaunchChild(connecter);
				return;
			}

			if (m_addr.Protocol.Equals("pgm") || m_addr.Protocol.Equals("epgm"))
			{
				PgmSender pgmSender = new PgmSender(m_ioThread, m_options, m_addr);
				pgmSender.Init(m_addr.Resolved as PgmAddress);

				SendAttach(this, pgmSender);

				return;
			}

			Debug.Assert(false);
		}

		public override String ToString()
		{
			return base.ToString() + "[" + m_options.SocketId + "]";
		}

		public virtual void InEvent()
		{
			throw new NotSupportedException();

		}

		public virtual void OutEvent()
		{
			throw new NotSupportedException();

		}





	}
}
