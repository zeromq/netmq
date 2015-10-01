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

using System.Net.Sockets;

namespace NetMQ.Core
{
    /// <summary>
    /// This is an ITimerEvent, with InCompleted and OutCompleted callback-methods,
    /// used for implementing a Proactor pattern.
    /// </summary>
    internal interface IProactorEvents : ITimerEvent
    {
        /// <summary>
        /// This is the "Input-Completed" method - called by the I/O-thread when the file descriptor is ready for reading.
        /// </summary>
        /// <param name="socketError">this is set to any socket-error that has occurred</param>
        /// <param name="bytesTransferred">the number of bytes that are now ready to be read</param>
        void InCompleted(SocketError socketError, int bytesTransferred);

        /// <summary>
        /// This is the "Output-Completed" method - called by the I/O thread when the file descriptor is ready for writing.
        /// </summary>
        /// <param name="socketError">this is set to any socket-error that has occurred</param>
        /// <param name="bytesTransferred">the number of bytes that are now ready to be written</param>
        void OutCompleted(SocketError socketError, int bytesTransferred);
    }
}
