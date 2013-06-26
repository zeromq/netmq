/*  
    Copyright (c) 2011 250bpm s.r.o.
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

using System;
using System.Net.Sockets;

namespace NetMQ.zmq
{
	public class Reaper : ZObject , IPollEvents {

		//  Reaper thread accesses incoming commands via this mailbox.
		private readonly Mailbox mailbox;

		//  Handle associated with mailbox' file descriptor.
		private readonly Socket m_mailboxHandle;

		//  I/O multiplexing is performed using a poller object.
		private readonly Poller m_poller;

		//  Number of sockets being reaped at the moment.
		private int m_sockets;

		//  If true, we were already asked to terminate.
		private volatile bool m_terminating;
    
		private readonly String m_name;

		public Reaper(Ctx ctx, int threadId)
			: base(ctx, threadId)
		{

			m_sockets = 0;
			m_terminating = false;
			m_name = "reaper-" + threadId;
			m_poller = new Poller(m_name);

			mailbox = new Mailbox(m_name);
        
			m_mailboxHandle = mailbox.FD;
			m_poller.AddFD (m_mailboxHandle, this);
			m_poller.SetPollin (m_mailboxHandle);
		}

		public void Destroy () {
			m_poller.Destroy();
			mailbox.Close();
		}
    
		public Mailbox Mailbox {
			get { return mailbox; }
		}
    
		public void Start() {
			m_poller.Start();
        
		}

		public void Stop() {
			if (!m_terminating)
				SendStop ();
		}
    
		public void InEvent() {

			while (true) {

				//  Get the next command. If there is none, exit.
				Command cmd = mailbox.Recv (0);
				if (cmd == null)
					break;
                
				//  Process the command.
				cmd.Destination.ProcessCommand (cmd);
			}

		}
    
		public void OutEvent() {
			throw new NotSupportedException();    
		}
    
		public void TimerEvent(int id) {
			throw new NotSupportedException();
		}

		override
			protected void ProcessStop ()
		{
			m_terminating = true;

			//  If there are no sockets being reaped finish immediately.
			if (m_sockets == 0) {
				SendDone ();
				m_poller.RemoveFD (m_mailboxHandle);
				m_poller.Stop ();
			}
		}
    
		override
			protected void ProcessReap (SocketBase socket)
		{			
			//  Add the socket to the poller.
			socket.StartReaping (m_poller);

			++m_sockets;
		}



		override
			protected void ProcessReaped ()
		{
			--m_sockets;
			
			//  If reaped was already asked to terminate and there are no more sockets,
			//  finish immediately.
			if (m_sockets == 0 && m_terminating) {
				SendDone ();
				m_poller.RemoveFD (m_mailboxHandle);
				m_poller.Stop ();
            
			}
		}


	}
}
