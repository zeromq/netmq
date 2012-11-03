using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zmq
{
    public class ByteArraySegment
    {
        byte[] m_innerBuffer;
        int m_innerBufferPos;

        public ByteArraySegment(byte[] buffer)
        {
            m_innerBuffer = buffer;
            m_innerBufferPos = 0;
        }

        public ByteArraySegment(byte[] buffer, int offset)
        {
            m_innerBuffer = buffer;
            m_innerBufferPos = offset;
        }

        public ByteArraySegment(ByteArraySegment otherSegment)
        {
            m_innerBuffer = otherSegment.m_innerBuffer;
            m_innerBufferPos = otherSegment.m_innerBufferPos;
        }

        public ByteArraySegment(ByteArraySegment otherSegment, int offset)
        {
            m_innerBuffer = otherSegment.m_innerBuffer;
            m_innerBufferPos = otherSegment.m_innerBufferPos + offset;
        }

        public int Size
        {
            get
            {
                return m_innerBuffer.Length - m_innerBufferPos;
            }
        }

        public void AdvanceOffset(int delta)
        {
            m_innerBufferPos += delta;
        }

        public int Offset
        {
            get
            {
                return m_innerBufferPos;
            }
        }
        
        public void PutLong(long value, int i)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m_innerBuffer, i + m_innerBufferPos, 8);
        }

        public void PutInteger(int value, int i)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m_innerBuffer, i + m_innerBufferPos, 4);
        }

        public void PutString(string s, int length, int i)
        {        
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(s), 0, m_innerBuffer, m_innerBufferPos + i,length);
        }

        public void PutString(string s, int i)
        {
            PutString(s, s.Length, i);
        }


        public long GetLong(int i)
        {
            var value = BitConverter.ToInt64(m_innerBuffer, i + m_innerBufferPos);

            return value;
        }
        
        public int GetInteger(int i)
        {
            var value = BitConverter.ToInt32(m_innerBuffer, i + m_innerBufferPos);

            return value;
        }

        public string GetString(int length, int i)
        {
            return Encoding.ASCII.GetString(m_innerBuffer, Offset + i, length);
        }

        public void CopyTo(ByteArraySegment otherSegment, int toCopy)
        {
            CopyTo(0, otherSegment, 0, toCopy);
        }

        public void CopyTo(int fromOffset, ByteArraySegment dest, int destOffset, int toCopy)
        {
            Buffer.BlockCopy(m_innerBuffer, Offset + fromOffset, dest.m_innerBuffer, dest.Offset + destOffset, toCopy);
        }


        
        public byte this[int i]
        {
            get { return m_innerBuffer[i + m_innerBufferPos]; }
            set { m_innerBuffer[i + m_innerBufferPos] = value; }
        }

        public static ByteArraySegment operator +(ByteArraySegment byteArray, int offset)
        {
            return new ByteArraySegment(byteArray, offset);
        }

        public static implicit operator ByteArraySegment(byte[] buffer)
        {
            return new ByteArraySegment(buffer);
        }

        public static explicit operator byte[](ByteArraySegment buffer)
        {
            return buffer.m_innerBuffer;
        }

        public override bool Equals(object obj)
        {
            if ((obj is ByteArraySegment))
            {
                ByteArraySegment other = (ByteArraySegment)obj;

                return m_innerBuffer == other.m_innerBuffer && m_innerBufferPos == other.m_innerBufferPos;
            }
            else if (obj is byte[])
            {
                byte[] byteArray = (byte[])obj;

                return byteArray == m_innerBuffer && m_innerBufferPos == 0;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int value = m_innerBuffer.GetHashCode() * 27;
            value += m_innerBufferPos;

            return value;
        }
    }
}
