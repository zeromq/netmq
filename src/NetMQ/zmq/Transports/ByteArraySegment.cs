using System;
using System.Text;
using JetBrains.Annotations;

namespace NetMQ.zmq.Transports
{
    internal sealed class ByteArraySegment
    {
        [NotNull] private readonly byte[] m_innerBuffer;

        public ByteArraySegment([NotNull] byte[] buffer)
        {
            m_innerBuffer = buffer;
            Offset = 0;
        }

        public ByteArraySegment([NotNull] byte[] buffer, int offset)
        {
            m_innerBuffer = buffer;
            Offset = offset;
        }

        public ByteArraySegment([NotNull] ByteArraySegment otherSegment)
        {
            m_innerBuffer = otherSegment.m_innerBuffer;
            Offset = otherSegment.Offset;
        }

        public ByteArraySegment([NotNull] ByteArraySegment otherSegment, int offset)
        {
            m_innerBuffer = otherSegment.m_innerBuffer;
            Offset = otherSegment.Offset + offset;
        }

        public int Size
        {
            get { return m_innerBuffer.Length - Offset; }
        }

        public void AdvanceOffset(int delta)
        {
            Offset += delta;
        }

        public int Offset { get; private set; }

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

        public void PutString([NotNull] string s, int length, int i)
        {
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(s), 0, m_innerBuffer, Offset + i, length);
        }

        public void PutString([NotNull] string s, int i)
        {
            PutString(s, s.Length, i);
        }


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

        [NotNull]
        public string GetString(int length, int i)
        {
            return Encoding.ASCII.GetString(m_innerBuffer, Offset + i, length);
        }

        public void CopyTo([NotNull] ByteArraySegment otherSegment, int toCopy)
        {
            CopyTo(0, otherSegment, 0, toCopy);
        }

        public void CopyTo(int fromOffset, [NotNull] ByteArraySegment dest, int destOffset, int toCopy)
        {
            Buffer.BlockCopy(m_innerBuffer, Offset + fromOffset, dest.m_innerBuffer, dest.Offset + destOffset, toCopy);
        }

        [NotNull]
        public ByteArraySegment Clone()
        {
            return new ByteArraySegment(this);
        }

        public byte this[int i]
        {
            get { return m_innerBuffer[i + Offset]; }
            set { m_innerBuffer[i + Offset] = value; }
        }

        public void Reset()
        {
            Offset = 0;
        }

        #region Operator overloads

        public static ByteArraySegment operator +([NotNull] ByteArraySegment byteArray, int offset)
        {
            return new ByteArraySegment(byteArray, offset);
        }

        public static implicit operator ByteArraySegment([NotNull] byte[] buffer)
        {
            return new ByteArraySegment(buffer);
        }

        public static explicit operator byte[]([NotNull] ByteArraySegment buffer)
        {
            return buffer.m_innerBuffer;
        }

        #endregion

        #region Hash code and equality

        public override bool Equals(object obj)
        {
            var byteArraySegment = obj as ByteArraySegment;
            if (byteArraySegment != null)
                return m_innerBuffer == byteArraySegment.m_innerBuffer && Offset == byteArraySegment.Offset;

            var bytes = obj as byte[];
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
