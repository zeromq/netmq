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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NetMQ.zmq;
using NetMQ.zmq.Utils;

namespace NetMQ
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
        Invalid = 0,
        Min = 101,
        Empty=101,
        GC = 102,
        Pool = 103,
        Delimiter = 104,
        Max = 104
    }

    public struct Msg
    {
        private MsgFlags m_flags;
        private int m_size;

        private byte[] m_data;
        private AtomicCounter m_atomicCounter;
        private MsgType m_type;

        public bool IsIdentity
        {
            get { return (m_flags & MsgFlags.Identity) == MsgFlags.Identity; }
        }

        public bool IsDelimiter
        {
            get { return MsgType == MsgType.Delimiter; }
        }

        public int Size
        {
            get
            {
                return m_size;
            }
        }

        public bool HasMore
        {
            get { return (m_flags & MsgFlags.More) == MsgFlags.More; }
        }

        public MsgType MsgType
        {
            get { return m_type; }
        }

        public MsgFlags Flags
        {
            get
            {
                return m_flags;
            }
        }

        public byte[] Data
        {
            get { return m_data; }
        }

        public bool Check()
        {
            return MsgType >= MsgType.Min && MsgType <= MsgType.Max;
        }

        public void InitEmpty()
        {
            m_type = MsgType.Empty;
            m_flags = MsgFlags.None;
            m_size = 0;
            m_data = null;
            m_atomicCounter = null;
        }

        public void InitPool(int size)
        {
            m_type = MsgType.Pool;
            m_flags = MsgFlags.None;
            m_data = BufferPool.Take(size);
            m_size = size;

            m_atomicCounter = new AtomicCounter();
        }

        public void InitGC(byte[] data, int size)
        {
            m_type = MsgType.GC;
            m_flags = MsgFlags.None;
            m_data = data;
            m_size = size;
            m_atomicCounter = null;
        }

        public void InitDelimiter()
        {
            m_type = MsgType.Delimiter;
            m_flags = MsgFlags.None;
        }

        public void Close()
        {
            if (!Check())
            {
                throw new FaultException();
            }

            if (m_type == MsgType.Pool)
            {
                // if not shared or reference counter drop to zero
                if ((m_flags & MsgFlags.Shared) == 0 || m_atomicCounter.Decrement() == 0)
                {
                    BufferPool.Return(m_data);
                }

                m_atomicCounter.Dispose();
                m_atomicCounter = null;
            }

            m_data = null;

            //  Make the message invalid.
            m_type = MsgType.Invalid;
        }

        public void AddReferences(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            if (m_type == MsgType.Pool)
            {
                if (m_flags == MsgFlags.Shared)
                {
                    m_atomicCounter.Increase(amount);
                }
                else
                {
                    m_atomicCounter.Set(amount);
                    m_flags |= MsgFlags.Shared;
                }
            }
        }

        public void RemoveReferences(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            if (m_type != MsgType.Pool || (m_flags & MsgFlags.Shared) == 0)
            {
                Close();
                return;
            }

            if (m_atomicCounter.Decrement(amount) == 0)
            {
                m_atomicCounter.Dispose();
                m_atomicCounter = null;

                BufferPool.Return(m_data);
            }
        }

        public override String ToString()
        {
            return base.ToString() + "[" + MsgType + "," + Size + "," + m_flags + "]";
        }


        public void SetFlags(MsgFlags flags)
        {
            m_flags = m_flags | flags;
        }


        public void ResetFlags(MsgFlags f)
        {
            m_flags = m_flags & ~f;
        }       

        public void Put(byte[] src, int i, int len)
        {
            if (len == 0 || src == null)
                return;

            Buffer.BlockCopy(src, 0, m_data, i, len);
        }

        public void Put(byte b)
        {
            m_data[0] = b;
        }

        public void Put(byte b, int i)
        {
            m_data[i] = b;
        }        

        public void Copy(ref Msg src)
        {
            //  Check the validity of the source.
            if (!src.Check())
            {
                throw new FaultException();
            }

            Close();

            if (m_type == MsgType.Pool)
            {
                //  One reference is added to shared messages. Non-shared messages
                //  are turned into shared messages and reference count is set to 2.
                if (src.m_flags.HasFlag(MsgFlags.Shared))
                    src.m_atomicCounter.Increase(1);
                else
                {
                    src.m_flags |= MsgFlags.Shared;
                    src.m_atomicCounter.Set(2);
                }
            }

            this = src;
        }

        public void Move(ref Msg src)
        {
            //  Check the validity of the source.
            if (!src.Check())
            {
                throw new FaultException();
            }

            Close();

            this = src;

            src.InitEmpty();
        }
    }
}