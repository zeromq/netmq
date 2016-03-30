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
    /// An IpcListener is a TcpListener that also has an Address property and a SetAddress method.
    /// </summary>
    internal sealed class IpcListener : TcpListener
    {
        private readonly IpcAddress m_address;

        /// <summary>
        /// Create a new IpcListener with the given IOThread, socket, and Options.
        /// </summary>
        /// <param name="ioThread"></param>
        /// <param name="socket">the SocketBase to listen to</param>
        /// <param name="options">an Options value that dictates the settings for this IpcListener</param>
        public IpcListener([NotNull] IOThread ioThread, [NotNull] SocketBase socket, [NotNull] Options options)
            : base(ioThread, socket, options)
        {
            m_address = new IpcAddress();
        }

        /// <summary>
        /// Get the bound address for use with wildcards
        /// </summary>
        public override string Address => m_address.ToString();

        /// <summary>
        /// Set address to listen on.
        /// </summary>
        /// <param name="addr">a string denoting the address to listen to</param>
        public override void SetAddress(string addr)
        {
            m_address.Resolve(addr, false);

            base.SetAddress(m_address.Address.Address + ":" + m_address.Address.Port);
        }
    }
}
