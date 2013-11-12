/*
    Copyright (c) 2010 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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
using System.Linq;


namespace NetMQ.zmq
{
    public class Blob
    {

        private readonly byte[] m_buf;
        private int m_hash;

        public Blob(byte[] data)
        {
            m_buf = new byte[data.Length];
            data.CopyTo(m_buf, 0);
        }

        public Blob(int size)
        {
            m_buf = new byte[size];
        }

        public Blob Put(int pos, byte b)
        {
            m_buf[pos] = b;
            m_hash = 0;
            return this;
        }

        public Blob Put(int pos, byte[] data)
        {

            Buffer.BlockCopy(data, 0, m_buf, pos, data.Length);

            m_hash = 0;
            return this;
        }

        public int Size
        {
            get { return m_buf.Length; }
        }

        public byte[] Data
        {
            get { return m_buf; }
        }

        public override bool Equals(Object t)
        {
            if (t is Blob)
            {
                Blob b = (Blob)t;
                if (b.m_buf.Length != m_buf.Length)
                {
                    return false;
                }

                return m_buf.SequenceEqual(b.m_buf);
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (m_hash == 0)
            {
                foreach (byte b in m_buf)
                {
                    m_hash = 31 * m_hash + b;
                }
            }
            return m_hash;
        }
    }
}
