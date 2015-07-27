/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
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

using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    /// <summary>
    /// Abstract interface to be implemented by various engines.
    /// </summary>
    internal interface IEngine
    {
        /// <summary>
        /// Plug the engine to the session.
        /// </summary>
        void Plug([NotNull] IOThread ioThread, [NotNull] SessionBase session);

        /// <summary>
        /// Terminate and deallocate the engine. Note that 'detached'
        /// events are not fired on termination.
        /// </summary>
        void Terminate();

        /// <summary>
        /// This method is called by the session to signal that more
        /// messages can be written to the pipe.
        /// </summary>
        void ActivateIn();

        /// <summary>
        /// This method is called by the session to signal that there
        /// are messages to send available.
        /// </summary>
        void ActivateOut();
    }
}
