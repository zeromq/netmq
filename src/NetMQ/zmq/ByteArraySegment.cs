using System;
using System.Text;
using System.Linq;

namespace NetMQ.zmq
{
	public class ByteArraySegment
	{
		readonly byte[] m_innerBuffer;

		public ByteArraySegment(byte[] buffer)
		{
			m_innerBuffer = buffer;
			Offset = 0;
		}

		public ByteArraySegment(byte[] buffer, int offset)
		{
			m_innerBuffer = buffer;
			Offset = offset;
		}

		public ByteArraySegment(ByteArraySegment otherSegment)
		{
			m_innerBuffer = otherSegment.m_innerBuffer;
			Offset = otherSegment.Offset;
		}

		public ByteArraySegment(ByteArraySegment otherSegment, int offset)
		{
			m_innerBuffer = otherSegment.m_innerBuffer;
			Offset = otherSegment.Offset + offset;
		}

		public int Size
		{
			get
			{
				return m_innerBuffer.Length - Offset;
			}
		}

		public void AdvanceOffset(int delta)
		{
			Offset += delta;
		}

		public int Offset
		{
			get;
			private set;
		}

		public void PutLong(long value, int i)
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

		public void PutUnsingedShort(ushort value, int i)
		{
			Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m_innerBuffer, i + Offset, 2);
		}

		public void PutInteger(int value, int i)
		{
			Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m_innerBuffer, i + Offset, 4);
		}

		public void PutString(string s, int length, int i)
		{
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(s), 0, m_innerBuffer, Offset + i, length);
		}

		public void PutString(string s, int i)
		{
			PutString(s, s.Length, i);
		}


		public long GetLong(int i)
		{
			// we changed how NetMQ is serializing long to support zeromq, however we still want to support old releases of netmq
			// so we check if the MSB is not zero, in case it not zero we need to reorder the bits
			if (m_innerBuffer[i] != 0)
			{
				return BitConverter.ToInt64(m_innerBuffer, i + Offset);
			}
			else
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
		}

		public int GetInteger(int i)
		{
			var value = BitConverter.ToInt32(m_innerBuffer, i + Offset);

			return value;
		}

		public ushort GetUnsignedShort(int i)
		{
			var value = BitConverter.ToUInt16(m_innerBuffer, i + Offset);

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

		public ByteArraySegment Clone()
		{
			return new ByteArraySegment(this);
		}

		public byte this[int i]
		{
			get { return m_innerBuffer[i + Offset]; }
			set { m_innerBuffer[i + Offset] = value; }
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

				return m_innerBuffer == other.m_innerBuffer && Offset == other.Offset;
			}
			else if (obj is byte[])
			{
				byte[] byteArray = (byte[])obj;

				return byteArray == m_innerBuffer && Offset == 0;
			}
			else
			{
				return false;
			}
		}

		public override int GetHashCode()
		{
			int value = m_innerBuffer.GetHashCode() * 27;
			value += Offset;

			return value;
		}

		internal void Reset()
		{
			Offset = 0;
		}


	}
}
