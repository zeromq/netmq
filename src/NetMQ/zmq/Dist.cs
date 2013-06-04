/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2011 Other contributors as noted in the AUTHORS file


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

namespace NetMQ.zmq
{
	public class Dist {
		//  List of outbound pipes.		
		private readonly List<Pipe> m_pipes;

		//  Number of all the pipes to send the next message to.
		private int m_matching;

		//  Number of active pipes. All the active pipes are located at the
		//  beginning of the pipes array. These are the pipes the messages
		//  can be sent to at the moment.
		private int m_active;

		//  Number of pipes eligible for sending messages to. This includes all
		//  the active pipes plus all the pipes that we can in theory send
		//  messages to (the HWM is not yet reached), but sending a message
		//  to them would result in partial message being delivered, ie. message
		//  with initial parts missing.
		private int m_eligible;

		//  True if last we are in the middle of a multipart message.
		private bool m_more;
    
		public Dist() {
			m_matching = 0;
			m_active = 0;
			m_eligible = 0;
			m_more = false;
			m_pipes = new List<Pipe>();
		}
    
		//  Adds the pipe to the distributor object.
		public void Attach (Pipe pipe)
		{   
			//  If we are in the middle of sending a message, we'll add new pipe
			//  into the list of eligible pipes. Otherwise we add it to the list
			//  of active pipes. 
			if (m_more) {
				m_pipes.Add (pipe); 
				//pipes.swap (eligible, pipes.size () - 1);
				Utils.Swap(m_pipes, m_eligible, m_pipes.Count - 1);
				m_eligible++;
			}
			else {
				m_pipes.Add (pipe);
				//pipes.swap (active, pipes.size () - 1);
				Utils.Swap(m_pipes, m_active, m_pipes.Count - 1);
				m_active++;
				m_eligible++;
			}
		}
    
		//  Mark the pipe as matching. Subsequent call to send_to_matching
		//  will send message also to this pipe.
		public void Match(Pipe pipe) {
        
			int idx = m_pipes.IndexOf (pipe);
			//  If pipe is already matching do nothing.
			if (idx < m_matching)
				return;

			//  If the pipe isn't eligible, ignore it.
			if (idx >= m_eligible)
				return;

			//  Mark the pipe as matching.
			Utils.Swap( m_pipes, idx, m_matching);
			m_matching++;

		}
    

		//  Mark all pipes as non-matching.
		public void Unmatch() {
			m_matching = 0;
		}


		//  Removes the pipe from the distributor object.
		public void Terminated(Pipe pipe) {
			//  Remove the pipe from the list; adjust number of matching, active and/or
			//  eligible pipes accordingly.
			if (m_pipes.IndexOf (pipe) < m_matching)
				m_matching--;
			if (m_pipes.IndexOf (pipe) < m_active)
				m_active--;
			if (m_pipes.IndexOf (pipe) < m_eligible)
				m_eligible--;
			m_pipes.Remove(pipe);
		}

		//  Activates pipe that have previously reached high watermark.
		public void Activated(Pipe pipe) {
			//  Move the pipe from passive to eligible state.
			Utils.Swap (m_pipes, m_pipes.IndexOf (pipe), m_eligible);
			m_eligible++;

			//  If there's no message being sent at the moment, move it to
			//  the active state.
			if (!m_more) {
				Utils.Swap (m_pipes, m_eligible - 1, m_active);
				m_active++;
			}

		}

		//  Send the message to all the outbound pipes.
		public void SendToAll(Msg msg, SendReceiveOptions flags)
		{
			m_matching = m_active;
			SendToMatching (msg, flags);
		}

		//  Send the message to the matching outbound pipes.
		public void SendToMatching(Msg msg, SendReceiveOptions flags)
		{
			//  Is this end of a multipart message?
			bool msg_more = msg.HasMore;

			//  Push the message to matching pipes.
			Distribute (msg, flags);

			//  If mutlipart message is fully sent, activate all the eligible pipes.
			if (!msg_more)
				m_active = m_eligible;

			m_more = msg_more;			
		}

		//  Put the message to all active pipes.
		private void Distribute(Msg msg, SendReceiveOptions flags)
		{
			//  If there are no matching pipes available, simply drop the message.
			if (m_matching == 0) {
				return;
			}
        
			for (int i = 0; i < m_matching; ++i)
				if(!Write (m_pipes[i], msg))
					--i; //  Retry last write because index will have been swapped
		}
    
		public bool HasOut ()
		{
			return true;
		}

		//  Write the message to the pipe. Make the pipe inactive if writing
		//  fails. In such a case false is returned.
		private bool Write (Pipe pipe, Msg msg)
		{
			if (!pipe.Write (msg)) {
				Utils.Swap(m_pipes, m_pipes.IndexOf (pipe), m_matching - 1);
				m_matching--;
				Utils.Swap(m_pipes, m_pipes.IndexOf (pipe), m_active - 1);
				m_active--;
				Utils.Swap(m_pipes, m_active, m_eligible - 1);
				m_eligible--;
				return false;
			}
			if (!msg.HasMore)
				pipe.Flush ();
			return true;
		}




	}
}
