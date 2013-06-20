/*      
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file
        
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
	public class XPub : SocketBase
	{

		public class XPubSession : SessionBase
		{

			public XPubSession(IOThread ioThread, bool connect,
												 SocketBase socket, Options options, Address addr) :
				base(ioThread, connect, socket, options, addr)
			{

			}

		}

		//  List of all subscriptions mapped to corresponding pipes.
		private readonly Mtrie m_subscriptions;

		//  Distributor of messages holding the list of outbound pipes.
		private readonly Dist m_dist;

		// If true, send all subscription messages upstream, not just
		// unique ones
		bool m_verbose;

		//  True if we are in the middle of sending a multi-part message.
		private bool m_more;

		//  List of pending (un)subscriptions, ie. those that were already
		//  applied to the trie, but not yet received by the user.
		private readonly Queue<Blob> m_pending;

		private static readonly Mtrie.MtrieDelegate s_markAsMatching;
		private static readonly Mtrie.MtrieDelegate s_SendUnsubscription;

		static XPub()
		{
			s_markAsMatching = (pipe, data, arg) =>
			{
				XPub self = (XPub)arg;
				self.m_dist.Match(pipe);
			};

			s_SendUnsubscription = (pipe, data, arg) =>
			{

				XPub self = (XPub)arg;

				if (self.m_options.SocketType != ZmqSocketType.Pub)
				{

					//  Place the unsubscription to the queue of pending (un)sunscriptions
					//  to be retrived by the user later on.
					Blob unsub = new Blob(data.Length + 1);
					unsub.Put(0, (byte)0);
					unsub.Put(1, data);
					self.m_pending.Enqueue(unsub);

				}
			};
		}

		public XPub(Ctx parent, int threadId, int sid)
			: base(parent, threadId, sid)
		{

			m_options.SocketType = ZmqSocketType.Xpub;
			m_verbose = false;
			m_more = false;

			m_subscriptions = new Mtrie();
			m_dist = new Dist();
			m_pending = new Queue<Blob>();
		}

		protected override void XAttachPipe(Pipe pipe, bool icanhasall)
		{
			Debug.Assert(pipe != null);
			m_dist.Attach(pipe);

			//  If icanhasall_ is specified, the caller would like to subscribe
			//  to all data on this pipe, implicitly.
			if (icanhasall)
				m_subscriptions.Add(null, pipe);

			//  The pipe is active when attached. Let's read the subscriptions from
			//  it, if any.
			XReadActivated(pipe);
		}

		protected override void XReadActivated(Pipe pipe)
		{
			//  There are some subscriptions waiting. Let's process them.
			Msg sub;
			while ((sub = pipe.Read()) != null)
			{

				//  Apply the subscription to the trie.
				byte[] data = sub.Data;
				int size = sub.Size;
				if (size > 0 && (data[0] == 0 || data[0] == 1))
				{
					bool unique;
					if (data[0] == 0)
						unique = m_subscriptions.Remove(data, 1, pipe);
					else
						unique = m_subscriptions.Add(data, 1, pipe);

					//  If the subscription is not a duplicate, store it so that it can be
					//  passed to used on next recv call.
					if (m_options.SocketType == ZmqSocketType.Xpub && (unique || m_verbose))
						m_pending.Enqueue(new Blob(sub.Data));
				}
			}
		}

		protected override void XWriteActivated(Pipe pipe)
		{
			m_dist.Activated(pipe);
		}

		protected override void XSetSocketOption(ZmqSocketOptions option, Object optval)
		{
			if (option != ZmqSocketOptions.XpubVerbose)
			{
				throw InvalidException.Create();
			}
			m_verbose = (int)optval == 1;
		}

		protected override void XTerminated(Pipe pipe)
		{
			//  Remove the pipe from the trie. If there are topics that nobody
			//  is interested in anymore, send corresponding unsubscriptions
			//  upstream.


			m_subscriptions.RemoveHelper(pipe, s_SendUnsubscription, this);

			m_dist.Terminated(pipe);
		}

		protected override bool XSend(Msg msg, SendReceiveOptions flags)
		{
			bool msgMore = msg.HasMore;

			//  For the first part of multi-part message, find the matching pipes.
			if (!m_more)
				m_subscriptions.Match(msg.Data, msg.Size,
														s_markAsMatching, this);

			//  Send the message to all the pipes that were marked as matching
			//  in the previous step.
			m_dist.SendToMatching(msg, flags);

			//  If we are at the end of multi-part message we can mark all the pipes
			//  as non-matching.
			if (!msgMore)
				m_dist.Unmatch();

			m_more = msgMore;

			return true;
		}


		protected override bool XHasOut()
		{
			return m_dist.HasOut();
		}

		protected override bool XRecv(SendReceiveOptions flags, out Msg msg)
		{
			//  If there is at least one 
			if (m_pending.Count == 0)
			{
				msg = null;
				return false;
			}

			Blob first = m_pending.Dequeue();
			msg = new Msg(first.Data);
			
			return true;
		}

		protected override bool XHasIn()
		{
			return m_pending.Count != 0;
		}


	}
}
