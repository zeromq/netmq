/*
    Copyright (c) 2010 250bpm s.r.o.
    Copyright (c) 2010-2015 Other contributors as noted in the AUTHORS file

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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// Class Blob serves to hold a byte-array buffer and methods for creating and accessing it.
    /// Use NetMQFrame instead; Blob is still present simply for backward compatibility.
    /// </summary>
    [Obsolete("Use NetMQFrame instead of Blob")]
    public class Blob
    {
        [NotNull]
        private readonly byte[] m_buffer;
        private int m_hash;

        /// <summary>
        /// Create a new Blob object containing the given data, and with a data-buffer of the given size.
        /// </summary>
        /// <param name="data">the byte-array of data for this Blob to contain</param>
        /// <param name="size">the length in bytes to allocate to the data buffer</param>
        public Blob([NotNull] byte[] data, int size)
        {
            m_buffer = new byte[size];

            Buffer.BlockCopy(data, 0, m_buffer, 0, size);            
        }

        /// <summary>
        /// Create a new Blob object with a data-buffer of the given size.
        /// </summary>
        /// <param name="size">the length in bytes to allocate to the data buffer</param>
        public Blob(int size)
        {
            m_buffer = new byte[size];
        }

        /// <summary>
        /// Put the given byte into the data buffer at the indicated position.
        /// </summary>
        /// <param name="pos">the (zero-based) index of the data buffer to put the byte into</param>
        /// <param name="b">the byte to write into the buffer</param>
        /// <returns>a reference to this Blob so that other method-calls may be chained</returns>
        [NotNull]
        public Blob Put(int pos, byte b)
        {
            m_buffer[pos] = b;
            m_hash = 0;
            return this;
        }

        /// <summary>
        /// Put the given byte into the data buffer at the indicated position.
        /// </summary>
        /// <param name="pos">the (zero-based) index of the data buffer to put the byte into</param>
        /// <param name="data">the byte-array data to write into the data buffer</param>
        /// <param name="count">how many bytes of data to write into the data buffer</param>
        /// <returns>a reference to this Blob so that other method-calls may be chained</returns>
        [NotNull]
        public Blob Put(int pos, [NotNull] byte[] data, int count)
        {
            Buffer.BlockCopy(data, 0, m_buffer, pos, count);

            m_hash = 0;
            return this;
        }

        /// <summary>
        /// Get the length of the data-buffer (the number of bytes in the array).
        /// </summary>
        public int Size
        {
            get { return m_buffer.Length; }
        }

        /// <summary>
        /// Get the data buffer contained by this Blob, as a byte-array.
        /// </summary>
        [NotNull]
        public byte[] Data
        {
            get { return m_buffer; }
        }

        /// <summary>
        /// Return true if this Blob is equal to the given object.
        /// </summary>
        /// <param name="t">the other object to compare against</param>
        /// <returns>true if equal</returns>
        public override bool Equals([CanBeNull] object t)
        {
            var blob = t as Blob;
            if (blob != null)
            {
                if (blob.m_buffer.Length != m_buffer.Length)
                {
                    return false;
                }

                return m_buffer.SequenceEqual(blob.m_buffer);
            }
            return false;
        }

        /// <summary>
        /// Return an integer hash-code to use to uniquely identify this Blob object.
        /// </summary>
        /// <returns>the hash-code</returns>
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            if (m_hash == 0)
            {
                foreach (byte b in m_buffer)
                {
                    m_hash = (31 * m_hash) ^ b;
                }
            }
            return m_hash;
        }
    }
}
