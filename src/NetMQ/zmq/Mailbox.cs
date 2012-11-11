/*
    Copyright (c) 2010-2011 250bpm s.r.o.
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

//import org.slf4j.Logger;
//import org.slf4j.LoggerFactory;
namespace NetMQ.zmq
{
	public class Mailbox {

		//private static Logger LOG = LoggerFactory.getLogger(Mailbox.class);
    
		//  The pipe to store actual commands.
		private readonly YPipe<Command> m_cpipe;

		//  Signaler to pass signals from writer thread to reader thread.
		private readonly Signaler m_signaler;

		//  There's only one thread receiving from the mailbox, but there
		//  is arbitrary number of threads sending. Given that ypipe requires
		//  synchronised access on both of its endpoints, we have to synchronise
		//  the sending side.
		private readonly object m_sync;

		//  True if the underlying pipe is active, ie. when we are allowed to
		//  read commands from it.
		private bool m_active;
    
		// mailbox name, for better debugging
		private readonly String m_name;

		public Mailbox(String name) {
			m_cpipe = new YPipe<Command>(Config.CommandPipeGranularity, "mailbox");
			m_sync = new object();
			m_signaler = new Signaler();
        
			//  Get the pipe into passive state. That way, if the users starts by
			//  polling on the associated file descriptor it will get woken up when
			//  new command is posted.
        
			Command cmd = m_cpipe.Read ();
			Debug.Assert(cmd == null);
			m_active = false;
        
			m_name = name;
		}
    

    
		public System.Net.Sockets.Socket FD 
		{
			get { return m_signaler.FD; }
		}
    
		public void Send (Command cmd)
		{   
			bool ok = false;
			lock (m_sync){
				m_cpipe.Write (cmd, false);
				ok = m_cpipe.Flush ();
			}
        
			//if (LOG.isDebugEnabled())
			//    LOG.debug( "{} -> {} / {} {}", new Object[] { Thread.currentThread().getName(), cmd_, cmd_.arg , !ok});
        
			if (!ok) {
				m_signaler.Send ();
			}
		}
    
		public Command Recv (int timeout)
		{
			Command cmd_ = null;
			//  Try to get the command straight away.
			if (m_active) {
				cmd_ = m_cpipe.Read ();
				if (cmd_ != null) {
                
					return cmd_;
				}

				//  If there are no more commands available, switch into passive state.
				m_active = false;
				m_signaler.Recv ();
			}


			//  Wait for signal from the command sender.
			bool rc = m_signaler.WaitEvent (timeout);
			if (!rc)
				return null;

			//  We've got the signal. Now we can switch into active state.
			m_active = true;

			//  Get a command.
			cmd_ = m_cpipe.Read ();
			Debug.Assert(cmd_ != null);
        
			return cmd_;
		}

		public void Close () {
			m_signaler.Close();
		}
    
		public override String ToString() {
			return base.ToString() + "[" + m_name + "]";
		}
	}
}
