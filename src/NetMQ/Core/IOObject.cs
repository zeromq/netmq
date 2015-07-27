/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Core
{
    /// <summary>
    /// Simple base class for objects that live in I/O threads.
    /// It makes communication with the poller object easier and
    /// makes defining unneeded event handlers unnecessary.
    /// </summary>
    internal class IOObject : IProactorEvents
    {
        [CanBeNull]
        private IOThread m_ioThread;
        [CanBeNull]
        private IProactorEvents m_handler;

        /// <summary>
        /// Create a new IOObject object and plug it into the given IOThread.
        /// </summary>
        /// <param name="ioThread">the IOThread to plug this new IOObject into.</param>
        public IOObject([CanBeNull] IOThread ioThread)
        {
            if (ioThread != null)
                Plug(ioThread);
        }

        /// <summary>
        /// "Plug in" this IOObject to the given IOThread, - ie associate this with the specified IOThread.
        /// </summary>
        /// <param name="ioThread">the IOThread for this object to live in</param>
        /// <remarks>
        /// When migrating an object from one I/O thread to another, first
        /// unplug it, then migrate it, then plug it to the new thread.
        /// </remarks>
        public void Plug([NotNull] IOThread ioThread)
        {
            Debug.Assert(ioThread != null);

            m_ioThread = ioThread;
        }

        /// <summary>
        /// "Un-Plug" this IOObject from its current IOThread, and set its handler to null.
        /// </summary>
        /// <remarks>
        /// When migrating an object from one I/O thread to another, first
        /// unplug it, then migrate it, then plug it to the new thread.
        /// </remarks>
        public void Unplug()
        {
            Debug.Assert(m_ioThread != null);

            // Forget about old poller in preparation to be migrated
            // to a different I/O thread.
            m_ioThread = null;
            m_handler = null;
        }

        /// <summary>
        /// Add the given socket to the Proactor.
        /// </summary>
        /// <param name="socket">the AsyncSocket to add</param>
        public void AddSocket([NotNull] AsyncSocket socket)
        {
            m_ioThread.Proactor.AddSocket(socket, this);
        }

        /// <summary>
        /// Remove the given socket from the Proactor.
        /// </summary>
        /// <param name="socket">the AsyncSocket to remove</param>
        public void RemoveSocket([NotNull] AsyncSocket socket)
        {
            m_ioThread.Proactor.RemoveSocket(socket);
        }

        /// <summary>
        /// This method is called when a message receive operation has been completed. This forwards it on to the handler's InCompleted method.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public virtual void InCompleted(SocketError socketError, int bytesTransferred)
        {
            m_handler.InCompleted(socketError, bytesTransferred);
        }

        /// <summary>
        /// This method is called when a message Send operation has been completed. This forwards it on to the handler's OutCompleted method.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public virtual void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            m_handler.OutCompleted(socketError, bytesTransferred);
        }

        /// <summary>
        /// This is called when the timer expires.
        /// </summary>
        /// <param name="id">an integer used to identify the timer</param>
        public virtual void TimerEvent(int id)
        {
            m_handler.TimerEvent(id);
        }

        public void AddTimer(long timeout, int id)
        {
            m_ioThread.Proactor.AddTimer(timeout, this, id);
        }

        public void SetHandler([NotNull] IProactorEvents handler)
        {
            m_handler = handler;
        }

        public void CancelTimer(int id)
        {
            m_ioThread.Proactor.CancelTimer(this, id);
        }
    }
}
