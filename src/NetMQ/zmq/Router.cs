/*
    Copyright (c) 2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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

//TODO: This class uses O(n) scheduling. Rewrite it to use O(1) algorithm.
namespace NetMQ.zmq
{
	public class Router : SocketBase
	{

		public class RouterSession : SessionBase
		{
			public RouterSession(IOThread ioThread, bool connect,
													 SocketBase socket, Options options,
													 Address addr)
				: base(ioThread, connect, socket, options, addr)
			{

			}
		}

		//  Fair queueing object for inbound pipes.
		private readonly FQ m_fq;

		//  True iff there is a message held in the pre-fetch buffer.
		private bool m_prefetched;

		//  If true, the receiver got the message part with
		//  the peer's identity.
		private bool m_identitySent;

		//  Holds the prefetched identity.
		private Msg m_prefetchedId;

		//  Holds the prefetched message.
		private Msg m_prefetchedMsg;

		//  If true, more incoming message parts are expected.
		private bool m_moreIn;

		class Outpipe
		{
			public Outpipe(Pipe pipe, bool active)
			{
				Pipe = pipe;
				Active = active;
			}

			public Pipe Pipe { get; private set; }
			public bool Active;
		};

		//  We keep a set of pipes that have not been identified yet.
		private readonly HashSet<Pipe> m_anonymousPipes;

		//  Outbound pipes indexed by the peer IDs.
		private readonly Dictionary<Blob, Outpipe> m_outpipes;

		//  The pipe we are currently writing to.
		private Pipe m_currentOut;

		//  If true, more outgoing message parts are expected.
		private bool m_moreOut;

		//  Peer ID are generated. It's a simple increment and wrap-over
		//  algorithm. This value is the next ID to use (if not used already).
		private int m_nextPeerId;

		// If true, report EHOSTUNREACH to the caller instead of silently dropping 
		// the message targeting an unknown peer.
		private bool m_mandatory;


		public Router(Ctx parent, int threadId, int sid)
			: base(parent, threadId, sid)
		{

			m_prefetched = false;
			m_identitySent = false;
			m_moreIn = false;
			m_currentOut = null;
			m_moreOut = false;
			m_nextPeerId = Utils.GenerateRandom();
			m_mandatory = false;

			m_options.SocketType = ZmqSocketType.Router;


			m_fq = new FQ();
			m_prefetchedId = new Msg();
			m_prefetchedMsg = new Msg();

			m_anonymousPipes = new HashSet<Pipe>();
			m_outpipes = new Dictionary<Blob, Outpipe>();

			//  TODO: Uncomment the following line when ROUTER will become true ROUTER
			//  rather than generic router socket.
			//  If peer disconnect there's noone to send reply to anyway. We can drop
			//  all the outstanding requests from that peer.
			//  options.delay_on_disconnect = false;

			m_options.RecvIdentity = true;

		}

		protected override void XAttachPipe(Pipe pipe, bool icanhasall)
		{
			Debug.Assert(pipe != null);

			bool identityOk = IdentifyPeer(pipe);
			if (identityOk)
				m_fq.Attach(pipe);
			else
				m_anonymousPipes.Add(pipe);
		}


		protected override void XSetSocketOption(ZmqSocketOptions option, Object optval)
		{
			if (option != ZmqSocketOptions.RouterMandatory)
			{
				throw InvalidException.Create();
			}
			m_mandatory = (bool)optval;
		}



		protected override void XTerminated(Pipe pipe)
		{
			if (!m_anonymousPipes.Remove(pipe))
			{

				Outpipe old;

				m_outpipes.TryGetValue(pipe.Identity, out  old);
				m_outpipes.Remove(pipe.Identity);

				Debug.Assert(old != null);

				m_fq.Terminated(pipe);
				if (pipe == m_currentOut)
					m_currentOut = null;
			}
		}


		protected override void XReadActivated(Pipe pipe)
		{
			if (!m_anonymousPipes.Contains(pipe))
				m_fq.Activated(pipe);
			else
			{
				bool identityOk = IdentifyPeer(pipe);
				if (identityOk)
				{
					m_anonymousPipes.Remove(pipe);
					m_fq.Attach(pipe);
				}
			}
		}


		protected override void XWriteActivated(Pipe pipe)
		{
			foreach (var it in m_outpipes)
			{
				if (it.Value.Pipe == pipe)
				{
					Debug.Assert(!it.Value.Active);
					it.Value.Active = true;
					break;
				}
			}
			Debug.Assert(false);
		}


		protected override bool XSend(Msg msg, SendReceiveOptions flags)
		{
			//  If this is the first part of the message it's the ID of the
			//  peer to send the message to.
			if (!m_moreOut)
			{
				Debug.Assert(m_currentOut == null);

				//  If we have malformed message (prefix with no subsequent message)
				//  then just silently ignore it.
				//  TODO: The connections should be killed instead.
				if (msg.HasMore)
				{

					m_moreOut = true;

					//  Find the pipe associated with the identity stored in the prefix.
					//  If there's no such pipe just silently ignore the message, unless
					//  mandatory is set.
					Blob identity = new Blob(msg.Data);
					Outpipe op;

					if (m_outpipes.TryGetValue(identity, out op))
					{
						m_currentOut = op.Pipe;
						if (!m_currentOut.CheckWrite())
						{
							op.Active = false;
							m_currentOut = null;
							if (m_mandatory)
							{
								m_moreOut = false;
								return false;
							}
						}
					}
					else if (m_mandatory)
					{
						m_moreOut = false;
						throw NetMQException.Create(ErrorCode.EHOSTUNREACH);
					}
				}

				return true;
			}

			//  Check whether this is the last part of the message.
			m_moreOut = msg.HasMore;

			//  Push the message into the pipe. If there's no out pipe, just drop it.
			if (m_currentOut != null)
			{
				bool ok = m_currentOut.Write(msg);
				if (!ok)
					m_currentOut = null;
				else if (!m_moreOut)
				{
					m_currentOut.Flush();
					m_currentOut = null;
				}
			}
			else
			{
			}

			//  Detach the message from the data buffer.

			return true;
		}



		protected override bool XRecv(SendReceiveOptions flags, out Msg msg)
		{
			msg = null;
			if (m_prefetched)
			{
				if (!m_identitySent)
				{
					msg = m_prefetchedId;
					m_prefetchedId = null;
					m_identitySent = true;
				}
				else
				{
					msg = m_prefetchedMsg;
					m_prefetchedMsg = null;
					m_prefetched = false;
				}
				m_moreIn = msg.HasMore;
				return true;
			}

			Pipe[] pipe = new Pipe[1];

			bool isMessageAvailable = m_fq.RecvPipe(pipe, out msg);

			//  It's possible that we receive peer's identity. That happens
			//  after reconnection. The current implementation assumes that
			//  the peer always uses the same identity.
			//  TODO: handle the situation when the peer changes its identity.
			while (isMessageAvailable && msg != null && msg.IsIdentity)
				isMessageAvailable = m_fq.RecvPipe(pipe, out msg);

			if (!isMessageAvailable)
			{
				return false;
			}
			else if (msg == null)
			{
				return true;
			}

			Debug.Assert(pipe[0] != null);

			//  If we are in the middle of reading a message, just return the next part.
			if (m_moreIn)
				m_moreIn = msg.HasMore;
			else
			{
				//  We are at the beginning of a message.
				//  Keep the message part we have in the prefetch buffer
				//  and return the ID of the peer instead.
				m_prefetchedMsg = msg;

				m_prefetched = true;

				Blob identity = pipe[0].Identity;
				msg = new Msg(identity.Data);
				msg.SetFlags(MsgFlags.More);
				m_identitySent = true;
			}

			return true;
		}

		//  Rollback any message parts that were sent but not yet flushed.
		protected void Rollback()
		{

			if (m_currentOut != null)
			{
				m_currentOut.Rollback();
				m_currentOut = null;
				m_moreOut = false;
			}
		}


		protected override bool XHasIn()
		{
			//  If we are in the middle of reading the messages, there are
			//  definitely more parts available.
			if (m_moreIn)
				return true;

			//  We may already have a message pre-fetched.
			if (m_prefetched)
				return true;

			//  Try to read the next message.
			//  The message, if read, is kept in the pre-fetch buffer.
			Pipe[] pipe = new Pipe[1];

			bool isMessageAvailable = m_fq.RecvPipe(pipe, out m_prefetchedMsg);

			if (!isMessageAvailable)
			{
				return false;
			}


			//  It's possible that we receive peer's identity. That happens
			//  after reconnection. The current implementation assumes that
			//  the peer always uses the same identity.
			//  TODO: handle the situation when the peer changes its identity.
			while (m_prefetchedMsg != null && m_prefetchedMsg.IsIdentity)
			{
				isMessageAvailable = m_fq.RecvPipe(pipe, out m_prefetchedMsg);

				if (!isMessageAvailable)
				{
					return false;
				}
			}

			if (m_prefetchedMsg == null)
			{
				return false;
			}

			Debug.Assert(pipe[0] != null);

			Blob identity = pipe[0].Identity;
			m_prefetchedId = new Msg(identity.Data);
			m_prefetchedId.SetFlags(MsgFlags.More);

			m_prefetched = true;
			m_identitySent = false;

			return true;
		}


		protected override bool XHasOut()
		{
			//  In theory, ROUTER socket is always ready for writing. Whether actual
			//  attempt to write succeeds depends on whitch pipe the message is going
			//  to be routed to.
			return true;
		}

		private bool IdentifyPeer(Pipe pipe)
		{
			Blob identity;

			Msg msg = pipe.Read();
			if (msg == null)
				return false;

			if (msg.Size == 0)
			{
				//  Fall back on the auto-generation
				byte[] buf = new byte[5];

				buf[0] = 0;

				byte[] result = BitConverter.GetBytes(m_nextPeerId++);
				//if (BitConverter.IsLittleEndian)
				//{
				//    Array.Reverse(result);
				//}

				Buffer.BlockCopy(result, 0, buf, 1, 4);
				identity = new Blob(buf);
			}
			else
			{
				identity = new Blob(msg.Data);

				//  Ignore peers with duplicate ID.
				if (m_outpipes.ContainsKey(identity))
					return false;
			}

			pipe.Identity = identity;
			//  Add the record into output pipes lookup table
			Outpipe outpipe = new Outpipe(pipe, true);
			m_outpipes.Add(identity, outpipe);

			return true;
		}


	}
}
