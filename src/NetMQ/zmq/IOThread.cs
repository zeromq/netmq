/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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
using System.Diagnostics;
using System.Net.Sockets;

namespace NetMQ.zmq
{
	public class IOThread : ZObject, IPollEvents {

		//  I/O thread accesses incoming commands via this mailbox.
		private readonly Mailbox m_mailbox;

		//  Handle associated with mailbox' file descriptor.
		private readonly Socket m_mailboxHandle;

		//  I/O multiplexing is performed using a poller object.
		private readonly Poller m_poller;

		readonly String m_name;
    
		public IOThread(Ctx ctx, int threadId) :base(ctx, threadId){			

			m_name = "iothread-" + threadId;
			m_poller = new Poller(m_name);

			m_mailbox = new Mailbox(m_name);
			m_mailboxHandle = m_mailbox.FD;
			m_poller.AddFD (m_mailboxHandle, this);
			m_poller.SetPollin (m_mailboxHandle);
        
		}
    
		public void Start() {
			m_poller.Start();
		}
    
		public void Destroy() {
			m_poller.Destroy();
			m_mailbox.Close();
		}
		public void Stop ()
		{
			SendStop ();
		}

		public Mailbox Mailbox {
			get
			{
				return m_mailbox;
			}
		}

    
		public int Load 
		{
			get { return m_poller.Load; }
		}

		public void InEvent() {
			//  TODO: Do we want to limit number of commands I/O thread can
			//  process in a single go?

			while (true) {

				//  Get the next command. If there is none, exit.
				Command cmd = m_mailbox.Recv (0);
				if (cmd == null)
					break;

				//  Process the command.
            
				cmd.Destination.ProcessCommand (cmd);
			}

		}
    
		public virtual void OutEvent() {
			throw new NotSupportedException();
		}  

		public virtual void TimerEvent(int id)
		{
			throw new NotSupportedException();
		}


		public Poller GetPoller() {
			Debug.Assert(m_poller != null);
			return m_poller;
		}
    
		protected override void ProcessStop ()
		{
			m_poller.RemoveFD (m_mailboxHandle);
        
			m_poller.Stop ();

		}


	}
}
