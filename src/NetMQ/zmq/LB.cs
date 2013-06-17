/*
    Copyright (c) 2010-2011 250bpm s.r.o.
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

namespace NetMQ.zmq
{
	public class LB
	{

		//  List of outbound pipes.
		private readonly List<Pipe> m_pipes;

		//  Number of active pipes. All the active pipes are located at the
		//  beginning of the pipes array.
		private int m_active;

		//  Points to the last pipe that the most recent message was sent to.
		private int m_current;

		//  True if last we are in the middle of a multipart message.
		private bool m_more;

		//  True if we are dropping current message.
		private bool m_dropping;

		public LB()
		{
			m_active = 0;
			m_current = 0;
			m_more = false;
			m_dropping = false;

			m_pipes = new List<Pipe>();
		}

		public void Attach(Pipe pipe)
		{
			m_pipes.Add(pipe);
			Activated(pipe);
		}

		public void Terminated(Pipe pipe)
		{
			int index = m_pipes.IndexOf(pipe);

			//  If we are in the middle of multipart message and current pipe
			//  have disconnected, we have to drop the remainder of the message.
			if (index == m_current && m_more)
				m_dropping = true;

			//  Remove the pipe from the list; adjust number of active pipes
			//  accordingly.
			if (index < m_active)
			{
				m_active--;
				Utils.Swap(m_pipes, index, m_active);
				if (m_current == m_active)
					m_current = 0;
			}
			m_pipes.Remove(pipe);

		}

		public void Activated(Pipe pipe)
		{
			//  Move the pipe to the list of active pipes.
			Utils.Swap(m_pipes, m_pipes.IndexOf(pipe), m_active);
			m_active++;
		}

		public  bool Send(Msg msg, SendReceiveOptions flags)
		{
			//  Drop the message if required. If we are at the end of the message
			//  switch back to non-dropping mode.
			if (m_dropping)
			{

				m_more = msg.HasMore;
				m_dropping = m_more;

				msg.Close();
				return true;
			}

			while (m_active > 0)
			{
				if (m_pipes[m_current].Write(msg))
					break;

				Debug.Assert(!m_more);
				m_active--;
				if (m_current < m_active)
					Utils.Swap(m_pipes, m_current, m_active);
				else
					m_current = 0;
			}

			//  If there are no pipes we cannot send the message.
			if (m_active == 0)
			{
				return false;
			}

			//  If it's part of the message we can fluch it downstream and
			//  continue round-robinning (load balance).
			m_more = msg.HasMore;
			if (!m_more)
			{
				m_pipes[m_current].Flush();
				if (m_active > 1)
					m_current = (m_current + 1) % m_active;
			}

			return true;
		}

		public bool HasOut()
		{
			//  If one part of the message was already written we can definitely
			//  write the rest of the message.
			if (m_more)
				return true;

			while (m_active > 0)
			{

				//  Check whether a pipe has room for another message.
				if (m_pipes[m_current].CheckWrite())
					return true;

				//  Deactivate the pipe.
				m_active--;
				Utils.Swap(m_pipes, m_current, m_active);
				if (m_current == m_active)
					m_current = 0;
			}

			return false;
		}
	}
}
