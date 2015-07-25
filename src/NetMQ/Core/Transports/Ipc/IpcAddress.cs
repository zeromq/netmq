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

using System.Net;

namespace NetMQ.Core.Transports.Ipc
{
    internal class IpcAddress : Address.IZAddress
    {
        private string m_name;

        public override string ToString()
        {
            if (m_name == null)
                return string.Empty;

            return Protocol + "://" + m_name;
        }

        public void Resolve(string name, bool ip4Only)
        {
            m_name = name;

            int hash = name.GetHashCode();
            if (hash < 0)
                hash = -hash;
            hash = hash%55536;
            hash += 10000;

            Address = new IPEndPoint(IPAddress.Loopback, hash);
        }

        public IPEndPoint Address { get; private set; }

        public string Protocol => Core.Address.IpcProtocol;
    }
}
