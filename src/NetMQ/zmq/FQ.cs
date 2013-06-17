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

using System.Collections.Generic;
using System.Diagnostics;

//  Class manages a set of inbound pipes. On receive it performs fair
//  queueing so that senders gone berserk won't cause denial of
//  service for decent senders.
namespace NetMQ.zmq
{
	public class FQ {

		//  Inbound pipes.
		private readonly List<Pipe> m_pipes;
    
		//  Number of active pipes. All the active pipes are located at the
		//  beginning of the pipes array.
		private int m_active;

		//  Index of the next bound pipe to read a message from.
		private int m_current;

		//  If true, part of a multipart message was already received, but
		//  there are following parts still waiting in the current pipe.
		private bool m_more;
    
		public FQ () {
			m_active = 0;
			m_current = 0;
			m_more = false;
        
			m_pipes = new List<Pipe>();
		}
    
		public void Attach (Pipe pipe)
		{
			m_pipes.Add (pipe);
			Utils.Swap (m_pipes, m_active, m_pipes.Count - 1);
			m_active++;
		}
    
		public void Terminated (Pipe pipe)
		{
			int index = m_pipes.IndexOf (pipe);

			//  Remove the pipe from the list; adjust number of active pipes
			//  accordingly.
			if (index < m_active) {
				m_active--;
				Utils.Swap (m_pipes, index, m_active);
				if (m_current == m_active)
					m_current = 0;
			}
			m_pipes.Remove (pipe);
		}

		public void Activated (Pipe pipe)
		{
			//  Move the pipe to the list of active pipes.
			Utils.Swap(m_pipes, m_pipes.IndexOf (pipe), m_active);
			m_active++;
		}

		public bool Recv (out Msg msg)
		{
			return RecvPipe(null, out msg);
		}

		public bool RecvPipe(Pipe[] pipe, out Msg msg_) {
			//  Deallocate old content of the message.			

			//  Round-robin over the pipes to get the next message.
			while (m_active > 0) {

				//  Try to fetch new message. If we've already read part of the message
				//  subsequent part should be immediately available.
				msg_ = m_pipes[m_current].Read ();

				bool fetched = msg_ != null;

				//  Note that when message is not fetched, current pipe is deactivated
				//  and replaced by another active pipe. Thus we don't have to increase
				//  the 'current' pointer.
				if (fetched) {
					if (pipe != null)
						pipe[0] = m_pipes[m_current];
					m_more = msg_.HasMore;
					if (!m_more)
						m_current = (m_current + 1) % m_active;
					return true;
				}
            
				//  Check the atomicity of the message.
				//  If we've already received the first part of the message
				//  we should get the remaining parts without blocking.
				Debug.Assert(!m_more);
            
				m_active--;
				Utils.Swap (m_pipes, m_current, m_active);
				if (m_current == m_active)
					m_current = 0;
			}

			//  No message is available. Initialise the output parameter
			//  to be a 0-byte message.
			msg_ = null;
			return false;
		}

		public bool HasIn ()
		{
			//  There are subsequent parts of the partly-read message available.
			if (m_more)
				return true;

			//  Note that messing with current doesn't break the fairness of fair
			//  queueing algorithm. If there are no messages available current will
			//  get back to its original value. Otherwise it'll point to the first
			//  pipe holding messages, skipping only pipes with no messages available.
			while (m_active > 0) {
				if (m_pipes[m_current].CheckRead ())
					return true;

				//  Deactivate the pipe.
				m_active--;
				Utils.Swap (m_pipes, m_current, m_active);
				if (m_current == m_active)
					m_current = 0;
			}

			return false;
		}

	}
}
