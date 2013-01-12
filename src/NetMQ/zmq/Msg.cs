/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

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
using System.Text;

namespace NetMQ.zmq
{
	[Flags]
	public enum MsgFlags
	{
		None = 0,
		More = 1,
		Identity = 64,
		Shared = 128,
	}

	[Flags]
	public enum MsgType : byte
	{
		Min = 101,
		Vsm = 102,
		LMsg = 103,
		Delimiter = 104,
		Max = 105
	}

	public class Msg
	{

		//  Size in bytes of the largest message that is still copied around
		//  rather than being reference-counted.


		//private byte type;
		private MsgFlags m_flags;
		private int m_size;
		private byte[] m_header;
		private byte[] m_data;
		private byte[] m_buf;

		public Msg()
		{
			Init(MsgType.Vsm);
		}

		public Msg(bool buffered)
		{
			if (buffered)
				Init(MsgType.LMsg);
			else
				Init(MsgType.Vsm);
		}

		public Msg(int size)
		{
			Init(MsgType.Vsm);
			Size = size;
		}

		public Msg(int size, bool buffered)
		{
			if (buffered)
				Init(MsgType.LMsg);
			else
				Init(MsgType.Vsm);
			Size = size;
		}


		public Msg(Msg m)
		{
			Clone(m);
		}

		public Msg(byte[] src)
			: this(src, false)
		{

		}

		public Msg(String src)
			: this(Encoding.ASCII.GetBytes(src), false)
		{

		}

		public Msg(byte[] src, bool copy) : this(src, src.Length, copy)
		{
			
		}

		public Msg(byte[] src,int size, bool copy)
			: this()
		{
			if (src != null)
			{
				Size = size;
				if (copy)
				{
					m_data = new byte[size];
					Buffer.BlockCopy(src, 0, m_data, 0, size);
				}
				else
				{
					m_data = src;
				}
			}
		}

		public bool IsIdentity
		{
			get { return (m_flags & MsgFlags.Identity) == MsgFlags.Identity; }
		}

		public bool IsDelimiter
		{
			get { return MsgType == MsgType.Delimiter; }
		}

		public bool Check()
		{
			return MsgType >= MsgType.Min && MsgType <= MsgType.Max;
		}

		private void Init(MsgType type)
		{
			this.MsgType = type;
			m_flags = MsgFlags.None;
			Size = 0;
			m_data = null;
			m_buf = null;
			m_header = null;
		}

		public int Size
		{
			get
			{
				return m_size;
			}
			set
			{
				m_size = value;
				if (MsgType == MsgType.LMsg)
				{
					m_flags = MsgFlags.None;

					m_buf = new byte[value];
					m_data = null;
				}
				else
				{
					m_flags = 0;
					m_data = new byte[value];
					m_buf = null;
				}
			}
		}



		public bool HasMore
		{
			get { return (m_flags & MsgFlags.More) == MsgFlags.More; }
		}

		public MsgType MsgType
		{
			get;
			set;
		}

		public MsgFlags Flags
		{
			get
			{
				return m_flags;
			}
		}

		public void SetFlags(MsgFlags flags)
		{
			m_flags = m_flags | flags;
		}

		public void InitDelimiter()
		{
			MsgType = MsgType.Delimiter;
			m_flags = MsgFlags.None;
		}


		public byte[] Data
		{
			get
			{
				if (m_data == null && MsgType == MsgType.LMsg)
					m_data = m_buf;
				return m_data;
			}
		}

		public int HeaderSize
		{
			get
			{
				if (m_header == null)
				{
					if (Size < 255)
						return 2;
					else
						return 10;
				}
				else if (m_header[0] == 0xff)
					return 10;
				else
					return 2;
			}
		}

		public byte[] Header
		{
			get
			{
				if (m_header == null)
				{
					if (Size < 255)
					{
						m_header = new byte[2];
						m_header[0] = (byte)Size;
						m_header[1] = (byte)m_flags;
					}
					else
					{
						m_header = new byte[10];

						m_header[0] = 0xff;
						m_header[1] = (byte)m_flags;

						Buffer.BlockCopy(BitConverter.GetBytes((long)Size), 0, m_header, 2, 8);
					}
				}
				return m_header;
			}
		}

		public void Close()
		{
			if (!Check())
			{
				throw NetMQException.Create(ErrorCode.EFAULT);
			}

			// In C++ version of zero mq close would free all bytes array and init the msg type
			// because in C# the GC is taking care or releasing resources and copying the message is expensive (because it's class and created on the heap)
			// the close method on C# version does nothing
			//Init(MsgType.Vsm);
		}

		public override String ToString()
		{
			return base.ToString() + "[" + MsgType + "," + Size + "," + m_flags + "]";
		}

		private void Clone(Msg m)
		{
			MsgType = m.MsgType;
			m_flags = m.m_flags;
			m_size = m.m_size;			
			m_buf = m.m_buf;
			m_data = m.m_data;
		}

		public void ResetFlags(MsgFlags f)
		{
			m_flags = m_flags & ~f;
		}

		public void Put(byte[] src, int i)
		{
			if (src == null)
				return;

			Buffer.BlockCopy(src, 0, m_data, i, src.Length);
		}

		public void Put(byte[] src, int i, int len)
		{
			if (len == 0 || src == null)
				return;

			Buffer.BlockCopy(src, 0, m_data, i, len);
		}

		public bool IsVsm
		{
			get
			{
				return MsgType == MsgType.Vsm;
			}
		}


		public void Put(byte b)
		{
			m_data[0] = b;
		}

		public void Put(byte b, int i)
		{
			m_data[i] = b;
		}

		public void Put(String str, int i)
		{
			Put(Encoding.ASCII.GetBytes(str), i);
		}

		public void Put(Msg data, int i)
		{
			Put(data.m_data, i);
		}


	}
}