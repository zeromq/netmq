/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011-2015 Other contributors as noted in the AUTHORS file

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
using NetMQ.Core.Transports.Tcp;

namespace NetMQ.Core.Transports.Ipc
{
    /// <summary>
    /// IpcConnecter is a subclass of TcpConnector, which provides absolutely nothing beyond what TcpConnector does.
    /// </summary>
    internal sealed class IpcConnector : TcpConnector
    {
        public IpcConnector([NotNull] IOThread ioThread, [NotNull] SessionBase session, [NotNull] Options options, [NotNull] Address addr, bool wait)
            : base(ioThread, session, options, addr, wait)
        {}
    }
}
