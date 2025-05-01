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

using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace NetMQ.Core.Transports.Ipc
{
    internal class IpcAddress : Address.IZAddress
    {
        private string? m_name;

        public override string ToString()
        {
            if (m_name == null)
                return string.Empty;

            return Protocol + "://" + m_name;
        }

        public void Resolve(string name, bool ip4Only)
        {
            m_name = name;

            int hash = GetMurmurHash(name);
            if (hash < 0)
                hash = -hash;
            hash = hash%55536;
            hash += 10000;

            this.Address = new IPEndPoint(IPAddress.Loopback, hash);
        }

        /// <summary>
        /// Calculate hash values using the MurmurHash algorithm
        /// </summary>
        /// <param name="name">input</param>
        /// <returns>hash value</returns>
        private static int GetMurmurHash(string name)
        {
            const uint seed = 0xc58f1a7b; 
            const uint m = 0x5bd1e995;
            const int r = 24;

            uint hash = seed ^ (uint)name.Length;
            int length = name.Length;
            int currentIndex = 0;

            while (length >= 4)
            {
                uint k = (uint)(name[currentIndex] |
                                (name[currentIndex + 1] << 8) |
                                (name[currentIndex + 2] << 16) |
                                (name[currentIndex + 3] << 24));

                k *= m;
                k ^= k >> r;
                k *= m;

                hash *= m;
                hash ^= k;

                currentIndex += 4;
                length -= 4;
            }

            switch (length)
            {
                case 3:
                    hash ^= (uint)(name[currentIndex + 2] << 16);
                    goto case 2;
                case 2:
                    hash ^= (uint)(name[currentIndex + 1] << 8);
                    goto case 1;
                case 1:
                    hash ^= name[currentIndex];
                    hash *= m;
                    break;
            }

            hash ^= hash >> 13;
            hash *= m;
            hash ^= hash >> 15;
            return (int)hash;
        }

        [DisallowNull]
        public IPEndPoint? Address { get; private set; }

        public string Protocol => Core.Address.IpcProtocol;
    }
}
