using System;
using System.Text;

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
			get; private set;			
		}

		public void PutLong(long value, int i)
		{
			Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m_innerBuffer, i + Offset, 8);
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
			var value = BitConverter.ToInt64(m_innerBuffer, i + Offset);

			return value;
		}

		public int GetInteger(int i)
		{
			var value = BitConverter.ToInt32(m_innerBuffer, i + Offset);

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
