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

using System.Diagnostics;
//  Simple base class for objects that live in I/O threads.
//  It makes communication with the poller object easier and
//  makes defining unneeded event handlers unnecessary.
using System.Net.Sockets;

namespace NetMQ.zmq
{
	public class IOObject : IPollEvents {

		private Poller m_poller;
		private IPollEvents m_handler;
    
		public IOObject(IOThread ioThread) {
			if (ioThread != null) {
				Plug(ioThread);
			}
		}

		//  When migrating an object from one I/O thread to another, first
		//  unplug it, then migrate it, then plug it to the new thread.
    
		public void Plug(IOThread ioThread) {
        
        
			Debug.Assert(ioThread != null);
			Debug.Assert(m_poller == null);

			//  Retrieve the poller from the thread we are running in.
			m_poller = ioThread.GetPoller ();    
		}
    
		public void Unplug() {
			Debug.Assert(m_poller != null);

			//  Forget about old poller in preparation to be migrated
			//  to a different I/O thread.
			m_poller = null;
			m_handler = null;
		}

		public void AddFd (Socket fd)
		{
			m_poller.AddFD (fd, this);
		}
    
		public void RmFd(Socket handle) {
			m_poller.RemoveFD(handle);
		}
    
		public void SetPollin (Socket handle)
		{
			m_poller.SetPollin (handle);
		}

		public void SetPollout (System.Net.Sockets.Socket handle)
		{
			m_poller.SetPollout (handle);
		}    
    
		public void ResetPollin(System.Net.Sockets.Socket handle) {
			m_poller.ResetPollin (handle);
		}


		public void ResetPollout(System.Net.Sockets.Socket handle) {
			m_poller.ResetPollout (handle);
		}

		public virtual void InEvent() {
			m_handler.InEvent();
		}

		public virtual void OutEvent()
		{
			m_handler.OutEvent();
		}
    
    
		public virtual void TimerEvent(int id) {
			m_handler.TimerEvent(id);
		}
    
		public void AddTimer (long timeout, int id)
		{
			m_poller.AddTimer (timeout, this, id);
		}

		public void SetHandler(IPollEvents handler) {
			this.m_handler = handler;
		}




		public void CancelTimer(int id) {
			m_poller.CancelTimer(this, id);
		}



	}
}
