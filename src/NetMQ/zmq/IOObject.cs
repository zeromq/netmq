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
using AsyncIO;

namespace NetMQ.zmq
{
    public class IOObject : IProcatorEvents
    {
        private IOThread m_ioThread;
        private IProcatorEvents m_handler;

        public IOObject(IOThread ioThread)
        {            
            if (ioThread != null)
            {
                Plug(ioThread);
            }
        }

        //  When migrating an object from one I/O thread to another, first
        //  unplug it, then migrate it, then plug it to the new thread.

        public void Plug()
        {
            Plug(null);
        }

        public void Plug(IOThread ioThread)
        {
            Debug.Assert(ioThread != null);

            m_ioThread = ioThread;                        
        }

        public void Unplug()
        {
            Debug.Assert(m_ioThread != null);

            //  Forget about old poller in preparation to be migrated
            //  to a different I/O thread.
            m_ioThread = null;
            m_handler = null;
        }

        public void AddSocket(AsyncSocket socket)
        {
            m_ioThread.Proactor.AddSocket(socket, this);
        }

        public void RemoveSocket(AsyncSocket socket)
        {
            m_ioThread.Proactor.RemoveSocket(socket);
        }        

        public virtual void InCompleted(SocketError socketError, int bytesTransferred)
        {
            m_handler.InCompleted(socketError, bytesTransferred);
        }

        public virtual void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            m_handler.OutCompleted(socketError, bytesTransferred);
        }


        public virtual void TimerEvent(int id)
        {
            m_handler.TimerEvent(id);
        }

        public void AddTimer(long timeout, int id)
        {
            m_ioThread.Proactor.AddTimer(timeout, this, id);
        }

        public void SetHandler(IProcatorEvents handler)
        {
            this.m_handler = handler;
        }

        public void CancelTimer(int id)
        {
            m_ioThread.Proactor.CancelTimer(this, id);
        }
    }
}
