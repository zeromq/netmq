using System;
using System.Text;
using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    /// <summary>
    /// The class ByteArraySegment provides for containing a simple byte-array, an Offset property,
    /// and a variety of operations that may be performed upon it.
    /// </summary>
    internal sealed class ByteArraySegment
    {
        [NotNull] private readonly byte[] m_innerBuffer;

        /// <summary>
        /// Create a new ByteArraySegment containing the given byte-array buffer.
        /// </summary>
        /// <param name="buffer">the byte-array for the new ByteArraySegment to contain</param>
        public ByteArraySegment([NotNull] byte[] buffer)
        {
            m_innerBuffer = buffer;
            Offset = 0;
        }

        /// <summary>
        /// Create a new ByteArraySegment containing the given byte-array buffer with the given offset.
        /// </summary>
        /// <param name="buffer">the byte-array for the new ByteArraySegment to contain</param>
        /// <param name="offset">the value for the Offset property</param>
        public ByteArraySegment([NotNull] byte[] buffer, int offset)
        {
            m_innerBuffer = buffer;
            Offset = offset;
        }

        /// <summary>
        /// Create a new ByteArraySegment that is a shallow-copy of the given ByteArraySegment (with a reference to the same buffer).
        /// </summary>
        /// <param name="otherSegment">the source-ByteArraySegment to make a copy of</param>
        public ByteArraySegment([NotNull] ByteArraySegment otherSegment)
        {
            m_innerBuffer = otherSegment.m_innerBuffer;
            Offset = otherSegment.Offset;
        }

        /// <summary>
        /// Create a new ByteArraySegment that is a shallow-copy of the given ByteArraySegment (with a reference to the same buffer)
        /// but with a different offset.
        /// </summary>
        /// <param name="otherSegment">the source-ByteArraySegment to make a copy of</param>
        /// <param name="offset">a value for the Offset property that is distinct from that of the source ByteArraySegment</param>
        public ByteArraySegment([NotNull] ByteArraySegment otherSegment, int offset)
        {
            m_innerBuffer = otherSegment.m_innerBuffer;
            Offset = otherSegment.Offset + offset;
        }

        /// <summary>
        /// Get the number of bytes within the buffer that is past the Offset (ie, buffer-length minus offset).
        /// </summary>
        public int Size => m_innerBuffer.Length - Offset;

        /// <summary>
        /// Add the given value to the offset.
        /// </summary>
        /// <param name="delta">the delta value to add to the offset.</param>
        public void AdvanceOffset(int delta)
        {
            Offset += delta;
        }

        /// <summary>
        /// Get the offset into the buffer.
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Write the given 64-bit value into the buffer, at the position marked by the offset plus the given index i.
        /// </summary>
        /// <param name="endian">an Endianness to specify in which order to write the bytes</param>
        /// <param name="value">the 64-bit value to write into the byte-array buffer</param>
        /// <param name="i">the index position beyond the offset to start writing the bytes</param>
        public void PutLong(Endianness endian, long value, int i)
        {
            if (endian == Endianness.Big)
            {
                m_innerBuffer[i + Offset] = (byte)(((value) >> 56) & 0xff);
                m_innerBuffer[i + Offset + 1] = (byte)(((value) >> 48) & 0xff);
                m_innerBuffer[i + Offset + 2] = (byte)(((value) >> 40) & 0xff);
                m_innerBuffer[i + Offset + 3] = (byte)(((value) >> 32) & 0xff);
                m_innerBuffer[i + Offset + 4] = (byte)(((value) >> 24) & 0xff);
                m_innerBuffer[i + Offset + 5] = (byte)(((value) >> 16) & 0xff);
                m_innerBuffer[i + Offset + 6] = (byte)(((value) >> 8) & 0xff);
                m_innerBuffer[i + Offset + 7] = (byte)(value & 0xff);
            }
            else
            {
                m_innerBuffer[i + Offset + 7] = (byte)(((value) >> 56) & 0xff);
                m_innerBuffer[i + Offset + 6] = (byte)(((value) >> 48) & 0xff);
                m_innerBuffer[i + Offset + 5] = (byte)(((value) >> 40) & 0xff);
                m_innerBuffer[i + Offset + 4] = (byte)(((value) >> 32) & 0xff);
                m_innerBuffer[i + Offset + 3] = (byte)(((value) >> 24) & 0xff);
                m_innerBuffer[i + Offset + 2] = (byte)(((value) >> 16) & 0xff);
                m_innerBuffer[i + Offset + 1] = (byte)(((value) >> 8) & 0xff);
                m_innerBuffer[i + Offset] = (byte)(value & 0xff);
            }
        }

        /// <summary>
        /// Write the given 16-bit value into the buffer, at the position marked by the offset plus the given index i.
        /// </summary>
        /// <param name="endian">an Endianness to specify in which order to write the bytes</param>
        /// <param name="value">the 16-bit value to write into the byte-array buffer</param>
        /// <param name="i">the index position beyond the offset to start writing the bytes</param>
        public void PutUnsignedShort(Endianness endian, ushort value, int i)
        {
            if (endian == Endianness.Big)
            {
                m_innerBuffer[i + Offset] = (byte)(((value) >> 8) & 0xff);
                m_innerBuffer[i + Offset + 1] = (byte)(value & 0xff);
            }
            else
            {
                m_innerBuffer[i + Offset + 1] = (byte)(((value) >> 8) & 0xff);
                m_innerBuffer[i + Offset] = (byte)(value & 0xff);
            }
        }

        /// <summary>
        /// Write the given 32-bit value into the buffer, at the position marked by the offset plus the given index i.
        /// </summary>
        /// <param name="endian">an Endianness to specify in which order to write the bytes</param>
        /// <param name="value">the 32-bit value to write into the byte-array buffer</param>
        /// <param name="i">the index position beyond the offset to start writing the bytes</param>
        public void PutInteger(Endianness endian, int value, int i)
        {
            if (endian == Endianness.Big)
            {
                m_innerBuffer[i + Offset] = (byte)(((value) >> 24) & 0xff);
                m_innerBuffer[i + Offset + 1] = (byte)(((value) >> 16) & 0xff);
                m_innerBuffer[i + Offset + 2] = (byte)(((value) >> 8) & 0xff);
                m_innerBuffer[i + Offset + 3] = (byte)(value & 0xff);
            }
            else
            {
                m_innerBuffer[i + Offset + 3] = (byte)(((value) >> 24) & 0xff);
                m_innerBuffer[i + Offset + 2] = (byte)(((value) >> 16) & 0xff);
                m_innerBuffer[i + Offset + 1] = (byte)(((value) >> 8) & 0xff);
                m_innerBuffer[i + Offset] = (byte)(value & 0xff);
            }
        }

        /// <summary>
        /// Encode the given String into a byte-array using the ASCII encoding
        /// and write that into the buffer.
        /// </summary>
        /// <param name="s">the String to write to the buffer</param>
        /// <param name="length">the number of encoded bytes to copy into the buffer</param>
        /// <param name="i">an index-offset, in addition to the Offset property, that marks where in the buffer to start writing bytes to</param>
        public void PutString([NotNull] string s, int length, int i)
        {
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(s), 0, m_innerBuffer, Offset + i, length);
        }

        /// <summary>
        /// Encode the given String into a byte-array using the ASCII encoding
        /// and write that into the buffer.
        /// </summary>
        /// <param name="s">the String to write to the buffer</param>
        /// <param name="i">an index-offset, in addition to the Offset property, that marks where in the buffer to start writing bytes to</param>
        public void PutString([NotNull] string s, int i)
        {
            PutString(s, s.Length, i);
        }


        /// <summary>
        /// Return a 64-bit numeric value that is read from the buffer, starting at the position marked by the offset plus the given index i,
        /// based upon the given byte-ordering.
        /// </summary>
        /// <param name="endian">an Endianness to specify which byte-ordering to use to interpret the source bytes</param>
        /// <param name="i">the index position beyond the offset to start reading the bytes</param>
        /// <returns>a long that is read from the bytes of the buffer</returns>
        public long GetLong(Endianness endian, int i)
        {
            // we changed how NetMQ is serializing long to support zeromq, however we still want to support old releases of netmq
            // so we check if the MSB is not zero, in case it not zero we need to reorder the bits
            if (endian == Endianness.Big)
            {
                return
                    (((long)m_innerBuffer[i + Offset]) << 56) |
                    (((long)m_innerBuffer[i + Offset + 1]) << 48) |
                    (((long)m_innerBuffer[i + Offset + 2]) << 40) |
                    (((long)m_innerBuffer[i + Offset + 3]) << 32) |
                    (((long)m_innerBuffer[i + Offset + 4]) << 24) |
                    (((long)m_innerBuffer[i + Offset + 5]) << 16) |
                    (((long)m_innerBuffer[i + Offset + 6]) << 8) |
                    ((long)m_innerBuffer[i + Offset + 7]);
            }
            else
            {
                return
                (((long)m_innerBuffer[i + Offset + 7]) << 56) |
                (((long)m_innerBuffer[i + Offset + 6]) << 48) |
                (((long)m_innerBuffer[i + Offset + 5]) << 40) |
                (((long)m_innerBuffer[i + Offset + 4]) << 32) |
                (((long)m_innerBuffer[i + Offset + 3]) << 24) |
                (((long)m_innerBuffer[i + Offset + 2]) << 16) |
                (((long)m_innerBuffer[i + Offset + 1]) << 8) |
                ((long)m_innerBuffer[i + Offset + 0]);
            }
        }

        /// <summary>
        /// Return a 64-bit unsigned numeric value (ulong) that is read from the buffer, starting at the position marked by the offset plus the given index i,
        /// based upon the given byte-ordering.
        /// </summary>
        /// <param name="endian">an Endianness to specify which byte-ordering to use to interpret the source bytes</param>
        /// <param name="i">the index position beyond the offset to start reading the bytes</param>
        /// <returns>an unsigned long that is read from the bytes of the buffer</returns>
        public ulong GetUnsignedLong(Endianness endian, int i)
        {
            // we changed how NetMQ is serializing long to support zeromq, however we still want to support old releases of netmq
            // so we check if the MSB is not zero, in case it not zero we need to reorder the bits
            if (endian == Endianness.Big)
            {
                return
                    (((ulong)m_innerBuffer[i + Offset]) << 56) |
                    (((ulong)m_innerBuffer[i + Offset + 1]) << 48) |
                    (((ulong)m_innerBuffer[i + Offset + 2]) << 40) |
                    (((ulong)m_innerBuffer[i + Offset + 3]) << 32) |
                    (((ulong)m_innerBuffer[i + Offset + 4]) << 24) |
                    (((ulong)m_innerBuffer[i + Offset + 5]) << 16) |
                    (((ulong)m_innerBuffer[i + Offset + 6]) << 8) |
                    ((ulong)m_innerBuffer[i + Offset + 7]);
            }
            else
            {
                return
                (((ulong)m_innerBuffer[i + Offset + 7]) << 56) |
                (((ulong)m_innerBuffer[i + Offset + 6]) << 48) |
                (((ulong)m_innerBuffer[i + Offset + 5]) << 40) |
                (((ulong)m_innerBuffer[i + Offset + 4]) << 32) |
                (((ulong)m_innerBuffer[i + Offset + 3]) << 24) |
                (((ulong)m_innerBuffer[i + Offset + 2]) << 16) |
                (((ulong)m_innerBuffer[i + Offset + 1]) << 8) |
                ((ulong)m_innerBuffer[i + Offset + 0]);
            }
        }

        /// <summary>
        /// Return a 32-bit integer that is read from the buffer, starting at the position marked by the offset plus the given index i,
        /// based upon the given byte-ordering.
        /// </summary>
        /// <param name="endian">an Endianness to specify which byte-ordering to use to interpret the source bytes</param>
        /// <param name="i">the index position beyond the offset to start reading the bytes</param>
        /// <returns>an integer that is read from the bytes of the buffer</returns>
        public int GetInteger(Endianness endian, int i)
        {
            if (endian == Endianness.Big)
            {
                return
                     ((m_innerBuffer[i + Offset]) << 24) |
                     ((m_innerBuffer[i + Offset + 1]) << 16) |
                     ((m_innerBuffer[i + Offset + 2]) << 8) |
                     (m_innerBuffer[i + Offset + 3]);
            }
            else
            {
                return
                     ((m_innerBuffer[i + Offset + 3]) << 24) |
                     ((m_innerBuffer[i + Offset + 2]) << 16) |
                     ((m_innerBuffer[i + Offset + 1]) << 8) |
                     (m_innerBuffer[i + Offset]);
            }
        }

        /// <summary>
        /// Return a 16-bit unsigned integer (ushort) value that is read from the buffer, starting at the position marked by the offset plus the given index i,
        /// based upon the given byte-ordering.
        /// </summary>
        /// <param name="endian">an Endianness to specify which byte-ordering to use to interpret the source bytes</param>
        /// <param name="i">the index position beyond the offset to start reading the bytes</param>
        /// <returns>a ushort that is read from the bytes of the buffer</returns>
        public ushort GetUnsignedShort(Endianness endian, int i)
        {
            if (endian == Endianness.Big)
            {
                return (ushort)(((m_innerBuffer[i + Offset]) << 8) |
                     (m_innerBuffer[i + Offset + 1]));
            }
            else
            {
                return (ushort)(((m_innerBuffer[i + Offset + 1]) << 8) |
                    (m_innerBuffer[i + Offset]));
            }
        }

        /// <summary>
        /// Return a String that is read from the byte-array buffer, decoded using the ASCII encoding,
        /// starting at the position marked by the offset plus the given index i.
        /// </summary>
        /// <param name="length">the length of the part of the buffer to read</param>
        /// <param name="i">the index position beyond the offset to start reading the bytes</param>
        /// <returns>a String decoded from the bytes of the buffer</returns>
        [NotNull]
        public string GetString(int length, int i)
        {
            return Encoding.ASCII.GetString(m_innerBuffer, Offset + i, length);
        }

        /// <summary>
        /// Write the bytes of this ByteArraySegment to the specified destination-ByteArraySegment.
        /// </summary>
        /// <param name="otherSegment">the destination-ByteArraySegment</param>
        /// <param name="toCopy">the number of bytes to copy</param>
        public void CopyTo([NotNull] ByteArraySegment otherSegment, int toCopy)
        {
            CopyTo(0, otherSegment, 0, toCopy);
        }

        /// <summary>
        /// Write the bytes of this ByteArraySegment to the specified destination-ByteArraySegment.
        /// </summary>
        /// <param name="fromOffset">an offset within this source buffer to start copying from</param>
        /// <param name="dest">the destination-ByteArraySegment</param>
        /// <param name="destOffset">an offset within the destination buffer to start copying to</param>
        /// <param name="toCopy">the number of bytes to copy</param>
        public void CopyTo(int fromOffset, [NotNull] ByteArraySegment dest, int destOffset, int toCopy)
        {
            Buffer.BlockCopy(m_innerBuffer, Offset + fromOffset, dest.m_innerBuffer, dest.Offset + destOffset, toCopy);
        }

        /// <summary>
        /// Return a shallow-copy of this ByteArraySegment.
        /// </summary>
        /// <returns>a new ByteArraySegment that is a shallow-copy of this one</returns>
        [NotNull]
        public ByteArraySegment Clone()
        {
            return new ByteArraySegment(this);
        }

        /// <summary>
        /// Get or set the byte of the buffer at the i-th index position (zero-based).
        /// </summary>
        /// <param name="i">the index of the byte to access</param>
        public byte this[int i]
        {
            get => m_innerBuffer[i + Offset];
            set => m_innerBuffer[i + Offset] = value;
        }

        /// <summary>
        /// Set the offset to zero.
        /// </summary>
        public void Reset()
        {
            Offset = 0;
        }

        #region Operator overloads

        /// <summary>
        /// Return a new ByteArraySegment that has the data of this one, but with the given offset.
        /// </summary>
        /// <param name="byteArray">the source-ByteArraySegment</param>
        /// <param name="offset">the offset-value to give the new ByteArraySegment</param>
        /// <returns></returns>
        public static ByteArraySegment operator +([NotNull] ByteArraySegment byteArray, int offset)
        {
            return new ByteArraySegment(byteArray, offset);
        }

        /// <summary>
        /// Return a new ByteArraySegment that contains the given byte-array buffer.
        /// </summary>
        /// <param name="buffer">the source byte-array buffer</param>
        /// <returns>a new ByteArraySegment that contains the given buffer</returns>
        public static implicit operator ByteArraySegment([NotNull] byte[] buffer)
        {
            return new ByteArraySegment(buffer);
        }

        /// <summary>
        /// Return the byte-array buffer of this ByteArraySegment.
        /// </summary>
        /// <param name="buffer">the source-ByteArraySegment</param>
        /// <returns>the byte-array that is the buffer of this ByteArraySegment</returns>
        public static explicit operator byte[]([NotNull] ByteArraySegment buffer)
        {
            return buffer.m_innerBuffer;
        }

        #endregion

        #region Hash code and equality

        /// <summary>
        /// Return true if the given Object is a ByteArraySegment and has the same buffer and offset.
        /// </summary>
        /// <param name="otherObject">an Object to compare for equality to this one</param>
        /// <returns>true only if the otherObject has the same buffer and offset</returns>
        /// <remarks>
        /// The given Object is considered to be Equal if it also is a ByteArraySegment,
        /// and has the same Offset property value
        /// and it's buffer points to the SAME byte-array as the otherObject does.
        /// </remarks>
        public override bool Equals(object otherObject)
        {
            var byteArraySegment = otherObject as ByteArraySegment;
            if (byteArraySegment != null)
                return m_innerBuffer == byteArraySegment.m_innerBuffer && Offset == byteArraySegment.Offset;

            var bytes = otherObject as byte[];
            if (bytes != null)
                return bytes == m_innerBuffer && Offset == 0;

            return false;
        }

        public override int GetHashCode()
        {
            int value = m_innerBuffer.GetHashCode()*27;
            value += Offset;

            return value;
        }

        #endregion
    }
}
