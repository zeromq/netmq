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
using NetMQ.zmq.Utils;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// This flags enum-type is used to indicate characteristics of a Msg
    /// - including More, Identity, and Shared (the default is None).
    /// </summary>
    [Flags]
    public enum MsgFlags
    {
        None = 0,
        More = 1,
        Identity = 64,

        /// <summary>
        /// This bit indicates that the internal data is shared with other Msg objects.
        /// </summary>
        Shared = 128,
    }

    /// <summary>
    /// This flags enum-type indicates whether a Msg is Invalid, Empty, GC, Pool, or a Delimiter.
    /// </summary>
    [Flags]
    public enum MsgType : byte
    {
        Invalid = 0,

        /// <summary>
        /// This is the minimum value that any MsgType may have (101).
        /// </summary>
        Min = 101,

        Empty = 101,
        GC = 102,
        Pool = 103,
        Delimiter = 104,

        /// <summary>
        /// This is the maximum value that any MsgType may have (104).
        /// </summary>
        Max = 104
    }

    public struct Msg
    {
        /// <summary>
        /// This serves as a reference-counter for shared byte-array data.
        /// </summary>
        private AtomicCounter m_atomicCounter;

        /// <summary>
        /// Get whether the Identity bit is set on the Flags property.
        /// </summary>
        public bool IsIdentity
        {
            get { return (Flags & MsgFlags.Identity) == MsgFlags.Identity; }
        }

        /// <summary>
        /// Get whether the Delimiter bit is set on the Flags property.
        /// </summary>
        public bool IsDelimiter
        {
            get { return MsgType == MsgType.Delimiter; }
        }

        /// <summary>
        /// Get the number of bytes within the Data property.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Get the "Has-More" flag, which when set on a message-queue frame indicates that there are more frames to follow.
        /// </summary>
        public bool HasMore
        {
            get { return (Flags & MsgFlags.More) == MsgFlags.More; }
        }

        /// <summary>
        /// Get the MsgType flags-enum value, which indicates which of the Invalid, Empty, GC, Pool, or Delimiter bits are set.
        /// </summary>
        public MsgType MsgType { get; private set; }

        /// <summary>
        /// Get the flags-enum MsgFlags value, which indicates which of the More, Identity, or Shared bits are set.
        /// </summary>
        public MsgFlags Flags { get; private set; }

        /// <summary>
        /// Get the byte-array that represents the data payload of this Msg.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// Return true if the MsgType property is within the allowable range.
        /// </summary>
        /// <returns>true if the value of MsgType is 101..104</returns>
        public bool Check()
        {
            return MsgType >= MsgType.Min && MsgType <= MsgType.Max;
        }

        /// <summary>
        /// Clear this Msg to empty - ie, set MsgFlags to None, MsgType to Empty, and clear the Data.
        /// </summary>
        public void InitEmpty()
        {
            MsgType = MsgType.Empty;
            Flags = MsgFlags.None;
            Size = 0;
            Data = null;
            m_atomicCounter = null;
        }

        /// <summary>
        /// Initialize this Msg to be of MsgType.Pool, with a data-buffer of the given number of bytes.
        /// </summary>
        /// <param name="size">the number of bytes to allocate in the data-buffer</param>
        public void InitPool(int size)
        {
            MsgType = MsgType.Pool;
            Flags = MsgFlags.None;
            Data = BufferPool.Take(size);
            Size = size;

            m_atomicCounter = new AtomicCounter();
        }

        /// <summary>
        /// Initialize this Msg to be of MsgType.GC with the given data-buffer value.
        /// </summary>
        /// <param name="data">the byte-array of data to assign to the Msg's Data property</param>
        /// <param name="size">the number of bytes that are in the data byte-array</param>
        public void InitGC([NotNull] byte[] data, int size)
        {
            MsgType = MsgType.GC;
            Flags = MsgFlags.None;
            Data = data;
            Size = size;
            m_atomicCounter = null;
        }

        /// <summary>
        /// Set this Msg to be of MsgType.Delimiter with no bits set within MsgFlags.
        /// </summary>
        public void InitDelimiter()
        {
            MsgType = MsgType.Delimiter;
            Flags = MsgFlags.None;
        }

        /// <summary>
        /// Clear the Data and set the MsgType to Invalid.
        /// If this is not a shared-data Msg (MsgFlags.Shared is not set), or it is shared but the reference-counter has dropped to zero,
        /// then return the data back to the BufferPool.
        /// </summary>
        public void Close()
        {
            if (!Check())
            {
                throw new FaultException("In Msg.Close, Check failed.");
            }

            if (MsgType == MsgType.Pool)
            {
                // if not shared or reference counter drop to zero
                if ((Flags & MsgFlags.Shared) == 0 || m_atomicCounter.Decrement() == 0)
                {
                    BufferPool.Return(Data);
                }

                m_atomicCounter = null;
            }

            Data = null;

            //  Make the message invalid.
            MsgType = MsgType.Invalid;
        }

        /// <summary>
        /// If this Msg is of MsgType.Pool, then - add the given amount number to the reference-counter
        /// and set the shared-data Flags bit.
        /// If this is not a Pool Msg, this does nothing.
        /// </summary>
        /// <param name="amount">the number to add to the internal reference-counter</param>
        public void AddReferences(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            if (MsgType == MsgType.Pool)
            {
                if (Flags == MsgFlags.Shared)
                {
                    m_atomicCounter.Increase(amount);
                }
                else
                {
                    m_atomicCounter.Set(amount);
                    Flags |= MsgFlags.Shared;
                }
            }
        }

        /// <summary>
        /// If this Msg is of MsgType.Pool and is marked as Shared, then - subtract the given amount number from the reference-counter
        /// and, if that reaches zero - return the data to the shared-data pool.
        /// If this is not both a Pool Msg and also marked as Shared, this simply Closes this Msg.
        /// </summary>
        /// <param name="amount">the number to subtract from the internal reference-counter</param>
        public void RemoveReferences(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            if (MsgType != MsgType.Pool || (Flags & MsgFlags.Shared) == 0)
            {
                Close();
                return;
            }

            if (m_atomicCounter.Decrement(amount) == 0)
            {
                m_atomicCounter = null;

                BufferPool.Return(Data);
            }
        }

        /// <summary>
        /// Override the Object ToString method to show the object-type, and values of the MsgType, Size, and Flags properties.
        /// </summary>
        /// <returns>a string that provides some detail about this Msg's state</returns>
        public override String ToString()
        {
            return base.ToString() + "[" + MsgType + "," + Size + "," + Flags + "]";
        }

        /// <summary>
        /// Set the indicated Flags bits.
        /// </summary>
        /// <param name="flags">which Flags bits to set (More, Identity, or Shared)</param>
        public void SetFlags(MsgFlags flags)
        {
            Flags = Flags | flags;
        }

        /// <summary>
        /// Clear the indicated Flags bits.
        /// </summary>
        /// <param name="flags">which Flags bits to clear (More, Identity, or Shared)</param>
        public void ResetFlags(MsgFlags flags)
        {
            Flags = Flags & ~flags;
        }       

        /// <summary>
        /// Copy the given byte-array data to this Msg's Data buffer.
        /// </summary>
        /// <param name="src">the source byte-array to copy from</param>
        /// <param name="i">offset within the source to start copying from</param>
        /// <param name="len">the number of bytes to copy</param>
        public void Put([NotNull] byte[] src, int i, int len)
        {
            if (len == 0 || src == null)
                return;

            Buffer.BlockCopy(src, 0, Data, i, len);
        }

        /// <summary>
        /// Copy the given single byte to this Msg's Data buffer.
        /// </summary>
        /// <param name="b">the source byte to copy from</param>
        public void Put(byte b)
        {
            Data[0] = b;
        }

        /// <summary>
        /// Copy the given single byte to this Msg's Data buffer at the given array-index.
        /// </summary>
        /// <param name="b">the source byte to copy from</param>
        /// <param name="i">index within the internal Data array to copy that byte to</param>
        public void Put(byte b, int i)
        {
            Data[i] = b;
        }        

        /// <summary>
        /// Close this Msg, and effectively make this Msg a copy of the given source Msg
        /// by simply setting it to point to the given source Msg.
        /// If this is a Pool Msg, then this also increases the reference-counter and sets the Shared bit.
        /// </summary>
        /// <param name="src">the source Msg to copy from</param>
        public void Copy(ref Msg src)
        {
            //  Check the validity of the source.
            if (!src.Check())
            {
                throw new FaultException("In Msg.Copy, Check failed.");
            }

            Close();

            if (MsgType == MsgType.Pool)
            {
                //  One reference is added to shared messages. Non-shared messages
                //  are turned into shared messages and reference count is set to 2.
                if (src.Flags.HasFlag(MsgFlags.Shared))
                    src.m_atomicCounter.Increase(1);
                else
                {
                    src.Flags |= MsgFlags.Shared;
                    src.m_atomicCounter.Set(2);
                }
            }

            this = src;
        }

        /// <summary>
        /// Close this Msg and make it reference the given source Msg, and then clear the Msg to empty.
        /// </summary>
        /// <param name="src">the source-Msg to become</param>
        public void Move(ref Msg src)
        {
            //  Check the validity of the source.
            if (!src.Check())
            {
                throw new FaultException("In Msg.Move, Check failed.");
            }

            Close();

            this = src;

            src.InitEmpty();
        }
    }
}