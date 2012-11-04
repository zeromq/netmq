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
using NetMQ;
using System.Text;

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
    private byte[] header;
    private byte[] data;
    private byte[] buf;

    public Msg()
    {
        init(MsgType.Vsm);
    }

    public Msg(bool buffered)
    {
        if (buffered)
					init(MsgType.LMsg);
        else
					init(MsgType.Vsm);
    }

    public Msg(int size_)
    {
			init(MsgType.Vsm);
        size = size_;
    }

    public Msg(int size_, bool buffered)
    {
        if (buffered)
					init(MsgType.LMsg);
        else
					init(MsgType.Vsm);
        size = size_;
    }


    public Msg(Msg m)
    {
        clone(m);
    }

    public Msg(byte[] src)
        : this(src, false)
    {

    }

    public Msg(String src)
        : this(Encoding.ASCII.GetBytes(src), false)
    {

    }

    public Msg(byte[] src, bool copy)
        : this()
    {
        if (src != null)
        {
            size = src.Length;
            if (copy)
            {
                data = new byte[src.Length];
                Buffer.BlockCopy(src, 0, data, 0, src.Length);
            }
            else
            {
                data = src;
            }
        }
    }

    //public Msg (ByteBuffer src) 
    //{
    //    init (type_lmsg);
    //    buf = src.duplicate ();
    //    buf.rewind ();
    //    size = buf.remaining ();
    //}

    public bool is_identity()
    {
        return (m_flags & MsgFlags.Identity) == MsgFlags.Identity;
    }

    public bool is_delimiter()
    {
			return type == MsgType.Delimiter;
    }


    public bool check()
    {
			return type >= MsgType.Min && type <= MsgType.Max;
    }

    private void init(MsgType type_)
    {
        type = type_;
        m_flags = MsgFlags.None;
        size = 0;
        data = null;
        buf = null;
        header = null;
    }

    public int size
    {
        get
        {
            return m_size;
        }
        set
        {
            m_size = value;
						if (type == MsgType.LMsg)
            {
                m_flags = MsgFlags.None;

                buf = new byte[value];
                data = null;
            }
            else
            {
                m_flags = 0;
                data = new byte[value];
                buf = null;
            }
        }
    }

 

    public bool has_more()
    {
        return (m_flags & MsgFlags.More) == MsgFlags.More;
    }

		public MsgType type
    {
        get;
        set;
    }

		public MsgFlags flags
    {
        get
        {
            return m_flags;
        }
    }

		public void SetFlags(MsgFlags flags_)
    {
        m_flags = m_flags | flags_;
    }

    public void init_delimiter()
    {
			type = MsgType.Delimiter;
        m_flags = MsgFlags.None;
    }


    public byte[] get_data()
    {
			if (data == null && type == MsgType.LMsg)
            data = buf;
        return data;
    }    

    public int header_size()
    {
        if (header == null)
        {
            if (size < 255)
                return 2;
            else
                return 10;
        }
        else if (header[0] == 0xff)
            return 10;
        else
            return 2;
    }

    public byte[] get_header()
    {
        if (header == null)
        {
            if (size < 255)
            {
                header = new byte[2];
                header[0] = (byte)size;
                header[1] = (byte)m_flags;
            }
            else
            {
                header = new byte[10];
                
                header[0] = 0xff;
                header[1] = (byte)m_flags;
                
                Buffer.BlockCopy(BitConverter.GetBytes((long)size), 0, header, 2,  8);
            }
        }
        return header;

    }    

    public void close()
    {
        if (!check())
        {
            throw new InvalidOperationException();
        }

				init(MsgType.Vsm);
    }

    public override String ToString()
    {
        return base.ToString() + "[" + type + "," + size + "," + m_flags + "]";
    }

    private void clone(Msg m)
    {
        type = m.type;
        m_flags = m.m_flags;
        size = m.size;
        buf = m.buf;
        data = m.data;
    }

    public void reset_flags(MsgFlags f)
    {
        m_flags = m_flags & ~f;
    }

    public void put(byte[] src, int i)
    {
        if (src == null)
            return;

        Buffer.BlockCopy(src, 0, data, i, src.Length);
    }

    public void put(byte[] src, int i, int len_)
    {

        if (len_ == 0 || src == null)
            return;

        Buffer.BlockCopy(src, 0, data, i, len_);
    }

    public bool is_vsm()
    {
			return type == MsgType.Vsm;
    }


    public void put(byte b)
    {
        data[0] = b;
    }

    public void put(byte b, int i)
    {
        data[i] = b;
    }

    public void put(String str, int i)
    {
        put(Encoding.ASCII.GetBytes(str), i);
    }

    public void put(Msg data, int i)
    {
        put(data.data, i);
    }


}
