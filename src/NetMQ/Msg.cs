/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NetMQ.Core.Utils;

namespace NetMQ
{
    /// <summary>Defines a set of flags applicable to a <see cref="Msg"/> instance: None (default), More, Identity, Shared</summary>
    [Flags]
    public enum MsgFlags : byte
    {
        /// <summary>Indicates no flags are set (the default).</summary>
        None = 0,

        /// <summary>Indicates that more frames of the same message follow.</summary>
        More = 1,
        
        /// <summary>
        /// Command frame (see ZMTP spec)
        /// Command types, use only bits 2-5 and compare with ==, not bitwise,
        /// a command can never be of more that one type at the same time 
        /// </summary>
        Command = 2,
        
        /// <summary>Indicates that this frame conveys the identity of a connected peer.</summary>
        Identity = 64,

        /// <summary>Indicates that this frame's internal data is shared with other <see cref="Msg"/> objects.</summary>
        Shared = 128,
    }

    /// <summary>Enumeration of possible <see cref="Msg"/> types: Uninitialised, GC, Pool, Delimiter.</summary>
    public enum MsgType : byte
    {
        /// <summary>The <see cref="Msg"/> has not yet been initialised (default value).</summary>
        Uninitialised = 0,

        /// <summary>The <see cref="Msg"/> is empty.</summary>
        Empty = 101,

        /// <summary>The <see cref="Msg"/> data will be garbage collected when no longer needed.</summary>
        GC = 102,
        
        /// <summary>
        /// Join message for radio-dish
        /// </summary>
        Join = 106,
        
        /// <summary>
        /// Leave message for radio_dish
        /// </summary>
        Leave = 107,

        /// <summary>
        /// The <see cref="Msg"/> data was allocated by <see cref="BufferPool"/>, and must be released back
        /// to this pool when no longer needed. This happens when <see cref="Msg.Close"/> is called.
        /// </summary>
        Pool = 103,

        /// <summary>The <see cref="Msg"/> is a delimiter frame and doesn't contain any data.</summary>
        /// <remarks>Delimiters are commonly used to mark a boundary between groups frames.</remarks>
        Delimiter = 104
    }

    /// <summary>
    /// A Msg struct is the lowest-level interpretation of a ZeroMQ message, and simply contains byte-array data
    /// and MsgType and MsgFlags properties.
    /// It supports message buffer pooling.
    /// </summary>
    /// <remarks>
    /// Many users will not use this class directly. However in high-performance situations it
    /// may be useful. When used correctly it's possible to have zero-copy and zero-allocation
    /// behaviour.
    /// </remarks>
    public struct Msg
    {
        /// <summary>
        /// The maximum length of a group (Radio/Dish)
        /// </summary>
        public const int MaxGroupLength = 255;
        
        /// <summary>An atomic reference count for knowing when to release a pooled data buffer back to the <see cref="BufferPool"/>.</summary>
        /// <remarks>Will be <c>null</c> unless <see cref="MsgType"/> equals <see cref="NetMQ.MsgType.Pool"/>.</remarks>
        private AtomicCounter? m_refCount;

        private byte[]? m_data;
        private int m_offset;
        private uint m_routingId;
        private string m_group;

        /// <summary>
        /// Get the number of bytes within the Data property.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Returns true if msg is empty
        /// </summary>
        public bool IsEmpty => m_data == null || Size == 0;

        /// <summary>
        /// Returns true if the msg is join message
        /// </summary>
        public bool IsJoin => MsgType == MsgType.Join;
        
        /// <summary>
        /// Returns true if the msg is leave message
        /// </summary>    
        public bool IsLeave => MsgType == MsgType.Leave;

        /// <summary>
        /// Gets the position of the first element in the Data property delimited by the message,
        /// relative to the start of the original array.
        /// Deprecated: use <see cref="Slice()"/> or implicit casting to Span
        /// </summary>
        [Obsolete("Use implicit casting to Span or Slice instead")]
        public int Offset => m_offset;

        #region MsgType

        /// <summary>Get the type of this message, from the MsgType enum.</summary>
        public MsgType MsgType { get; private set; }

        /// <summary>
        /// Get whether the Delimiter bit is set on the Flags property,
        /// which would indicate that this message is intended for use simply to mark a boundary
        /// between other parts of some unit of communication.
        /// </summary>
        public bool IsDelimiter => MsgType == MsgType.Delimiter;

        /// <summary>Get whether this <see cref="Msg"/> is initialised and ready for use.</summary>
        /// <remarks>A newly constructed <see cref="Msg"/> is uninitialised, and can be initialised via one
        /// of <see cref="InitEmpty"/>, <see cref="InitDelimiter"/>, <see cref="InitGC(byte[],int)"/>, <see cref="InitGC(byte[],int,int)"/>, or <see cref="InitPool"/>.
        /// Calling <see cref="Close"/> will cause the <see cref="Msg"/> to become uninitialised again.</remarks>
        /// <returns><c>true</c> if the <see cref="Msg"/> is initialised, otherwise <c>false</c>.</returns>
        public bool IsInitialised => MsgType != MsgType.Uninitialised;

        #endregion

        #region MsgFlags

        /// <summary>
        /// Get the flags-enum MsgFlags value, which indicates which of the More, Identity, or Shared bits are set.
        /// </summary>
        public MsgFlags Flags { get; private set; }

        /// <summary>
        /// Get the "Has-More" flag, which when set on a message-queue frame indicates that there are more frames to follow.
        /// </summary>
        public bool HasMore => (Flags & MsgFlags.More) == MsgFlags.More;
        
        internal bool HasCommand => (Flags & MsgFlags.Command) == MsgFlags.Command;
        
        /// <summary>
        /// Get whether the <see cref="Data"/> buffer of this <see cref="Msg"/> is shared with another instance.
        /// Only applies to pooled message types.
        /// </summary>
        public bool IsShared => (Flags & MsgFlags.Shared) != 0;

        /// <summary>
        /// Get whether the Identity bit is set on the Flags property.
        /// </summary>
        public bool IsIdentity => (Flags & MsgFlags.Identity) != 0;

        /// <summary>
        /// Set the indicated Flags bits.
        /// </summary>
        /// <param name="flags">which Flags bits to set (More, Identity, or Shared)</param>
        public void SetFlags(MsgFlags flags)
        {
            Flags |= flags;
        }

        /// <summary>
        /// Clear the indicated Flags bits.
        /// </summary>
        /// <param name="flags">which Flags bits to clear (More, Identity, or Shared)</param>
        public void ResetFlags(MsgFlags flags)
        {
            Flags &= ~flags;
        }

        #endregion

        /// <summary>
        /// Routing id of the message, for SERVER and PEER sockets only.
        /// </summary>
        public uint RoutingId
        {
            get => m_routingId;
            set
            {
                if (value == 0)
                    throw new ArgumentOutOfRangeException("RoutingId cannot be zero.");
                m_routingId = value;
            }
        }

        /// <summary>
        /// Reset routing id back to zero
        /// </summary>
        internal void ResetRoutingId()
        {
            m_routingId = 0;
        }

        /// <summary>
        /// Set or retrieve the group for RADIO/DISH sockets
        /// </summary>
        /// <exception cref="InvalidException">Value is larger than 255.</exception>
        public string Group
        {
            get => m_group;
            set
            {
                if (value.Length > MaxGroupLength)
                    throw new ArgumentOutOfRangeException("Group maximum length is 255");

                m_group = value;
            }
        }

        /// <summary>
        /// Get the byte-array that represents the data payload of this <see cref="Msg"/>.
        /// Deprecated: use <see cref="Slice()" /> or implicit casting to Span
        /// </summary>
        /// <remarks>
        /// This value will be <c>null</c> if <see cref="MsgType"/> is <see cref="NetMQ.MsgType.Uninitialised"/>,
        /// <see cref="NetMQ.MsgType.Empty"/> or <see cref="NetMQ.MsgType.Delimiter"/>.
        /// </remarks>
        [Obsolete("Use implicit casting to Span or Slice instead")]
        public byte[]? Data => m_data;
        
        /// <summary>
        /// Return the internal buffer as Span
        /// </summary>
        /// <returns>The span</returns>
        public Span<byte> Slice()
        {
            if (m_data == null)
                return Span<byte>.Empty;
            
            return new Span<byte>(m_data, m_offset, Size);  
        }
        
        /// <summary>
        /// Return the internal buffer as Memory
        /// </summary>
        /// <returns>The memory</returns>
        public Memory<byte> SliceAsMemory()
        {
            if (m_data == null)
                return Memory<byte>.Empty;
            
            return new Memory<byte>(m_data, m_offset, Size);  
        }

        /// <summary>
        /// Returns a slice of the internal buffer.
        /// </summary>
        /// <param name="offset">The offset to take the span from</param>
        public Span<byte> Slice(int offset)
        {
            if (m_data == null && offset > 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (m_offset + offset > Size)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            return new Span<byte>(m_data, m_offset + offset, Size - offset);  
        }

        /// <summary>
        /// Returns a slice of the internal buffer.
        /// </summary>
        /// <param name="offset">The offset to take the span from</param>
        /// <param name="count">The size of the slice</param>
        public Span<byte> Slice(int offset, int count)
        {
            if (m_data == null && offset > 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (m_offset + offset > Size)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (m_offset + offset + count > Size)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return new Span<byte>(m_data, m_offset + offset, count);
        }

        /// <summary>
        /// Copy the content of the message into a Span
        /// </summary>
        /// <param name="span">The span to copy content to</param>
        public void CopyTo(Span<byte> span)
        {
            ((Span<byte>) this).CopyTo(span);
        }
        
        /// <summary>
        /// Return a copy of the internal buffer as byte array
        /// </summary>
        /// <returns>Byte array</returns>
        public byte[] ToArray()
        {
            var data = new byte[Size];

            if (Size > 0)
                Buffer.BlockCopy(m_data!, m_offset, data, 0, Size);

            return data;
        }
        
        /// <summary>
        /// Implicit case of of Msg to Span
        /// </summary>
        /// <returns>Span</returns>
        public static implicit operator Span<byte>(Msg msg)
        {
            return new Span<byte>(msg.m_data, msg.m_offset, msg.Size);
        }
        
        /// <summary>
        /// Implicit case of of Msg to ReadOnlySpan
        /// </summary>
        /// <returns>Span</returns>
        public static implicit operator ReadOnlySpan<byte>(Msg msg)
        {
            return new Span<byte>(msg.m_data, msg.m_offset, msg.Size);
        }
        
        /// <summary>
        /// Returns span enumerator, to iterate of the Msg
        /// </summary>
        /// <returns>Span Enumerator</returns>
        public Span<byte>.Enumerator GetEnumerator()
        {
            return ((Span<byte>) this).GetEnumerator();
        }
        
        #region Initialisation

        /// <summary>
        /// Clear this Msg to empty - ie, set MsgFlags to None, MsgType to Empty, and clear the Data.
        /// </summary>
        public void InitEmpty()
        {
            MsgType = MsgType.Empty;
            Flags = MsgFlags.None;
            Size = 0;
            m_offset = 0;
            m_data = null;
            EnsureAtomicCounterNull();
        }

        /// <summary>
        /// Initialise this Msg to be of MsgType.Pool, with a data-buffer of the given number of bytes.
        /// </summary>
        /// <param name="size">the number of bytes to allocate in the data-buffer</param>
        public void InitPool(int size)
        {
            MsgType = MsgType.Pool;
            Flags = MsgFlags.None;
            m_data = BufferPool.Take(size);
            Size = size;
            m_offset = 0;
            EnsureAtomicCounterNull();
            m_refCount = AtomicCounterPool.Take();
        }

        /// <summary>
        /// Initialise this Msg to be of MsgType.GC with the given data-buffer value.
        /// </summary>
        /// <param name="data">the byte-array of data to assign to the Msg's Data property</param>
        /// <param name="size">the number of bytes that are in the data byte-array</param>
        public void InitGC(byte[] data, int size)
        {
            InitGC(data, 0, size);
        }

        /// <summary>
        /// Initialise this Msg to be of MsgType.GC with the given data-buffer value.
        /// </summary>
        /// <param name="data">the byte-array of data to assign to the Msg's Data property</param>
        /// <param name="offset">first byte in the data array</param>
        /// <param name="size">the number of bytes that are in the data byte-array</param>
        public void InitGC(byte[] data, int offset, int size)
        {
            MsgType = MsgType.GC;
            Flags = MsgFlags.None;
            m_data = data;
            Size = size;
            m_offset = offset;
            EnsureAtomicCounterNull();
        }

        /// <summary>
        /// Set this Msg to be of type MsgType.Delimiter with no bits set within MsgFlags.
        /// </summary>
        public void InitDelimiter()
        {
            MsgType = MsgType.Delimiter;
            Flags = MsgFlags.None;
        }

        internal void InitJoin()
        {
            MsgType = MsgType.Join;
            Flags = MsgFlags.None;
        }

        internal void InitLeave()
        {
            MsgType = MsgType.Leave;
            Flags = MsgFlags.None;
        }

        #endregion

        /// <summary>
        /// Clear the <see cref="Data"/> and set the MsgType to Invalid.
        /// If this is not a shared-data Msg (MsgFlags.Shared is not set), or it is shared but the reference-counter has dropped to zero,
        /// then return the data back to the BufferPool.
        /// </summary>
        /// <exception cref="FaultException">The object is not initialised.</exception>
        public void Close()
        {
            if (!IsInitialised)
                throw new FaultException("Cannot close an uninitialised Msg.");

            if (MsgType == MsgType.Pool)
            {
                Assumes.NotNull(m_refCount);
                Assumes.NotNull(m_data);

                // if not shared or reference counter drop to zero
                if (!IsShared || m_refCount.Decrement() == 0)
                    BufferPool.Return(m_data);

                EnsureAtomicCounterNull();
            }

            // Uninitialise the frame
            m_data = null;
            MsgType = MsgType.Uninitialised;
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
                Assumes.NotNull(m_refCount);

                if (IsShared)
                {
                    m_refCount.Increase(amount);
                }
                else
                {
                    // Because msg is not yet shared we add 1 to the amount to represent the current copy of the msg.
                    // We can set the amount in a none thread-safe way because the message is not yet shared and
                    // therefore only being used by one thread
                    m_refCount.Set(amount + 1);
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
                return;

            if (MsgType != MsgType.Pool || !IsShared)
            {
                Close();
                return;
            }

            Assumes.NotNull(m_refCount);

            if (m_refCount.Decrement(amount) == 0)
            {
                Assumes.NotNull(m_data);

                EnsureAtomicCounterNull();

                BufferPool.Return(m_data);

                // TODO shouldn't we set the type to uninitialised, or call clear, here? the object has a null refCount, but other methods may try to use it
            }
        }
        
        /// <summary>
        /// Override the Object ToString method to show the object-type, and values of the MsgType, Size, and Flags properties.
        /// </summary>
        /// <returns>a string that provides some detail about this Msg's state</returns>
        public override string ToString()
        {
            return base.ToString() + "[" + MsgType + "," + Size + "," + Flags + "]";
        }

        /// <summary>
        /// Convert the Msg to string
        /// </summary>
        /// <param name="encoding">The encoding to use for the conversion</param>
        /// <returns>The string</returns>
        public string GetString(Encoding encoding)
        {
            Assumes.NotNull(m_data);
            return encoding.GetString(m_data, m_offset, Size);
        }

        /// <summary>
        /// Convert the Msg to string
        /// </summary>
        /// <param name="encoding">The encoding to use for the conversion</param>
        /// <param name="offset">Offset to start conversion from</param>
        /// <param name="count">Number of bytes to convert</param>
        /// <returns>The string</returns>
        public string GetString(Encoding encoding, int offset, int count)
        {
            Assumes.NotNull(m_data);
            return encoding.GetString(m_data, m_offset + offset, count);
        }

        /// <summary>
        /// Copy the given byte-array data to this Msg's Data buffer.
        /// </summary>
        /// <param name="src">the source byte-array to copy from</param>
        /// <param name="dstOffset">index within the internal Data array to copy that byte to</param>
        /// <param name="len">the number of bytes to copy</param>
        public void Put(byte[]? src, int dstOffset, int len)
        {
            if (len == 0 || src == null)
                return;
            Assumes.NotNull(m_data);
            Buffer.BlockCopy(src, 0, m_data, dstOffset, len);
        }

        /// <summary>
        /// Copy the given byte-array data to this Msg's Data buffer.
        /// </summary>
        /// <param name="src">the source byte-array to copy from</param>
        /// <param name="srcOffset">first byte in the source byte-array</param>
        /// <param name="dstOffset">index within the internal Data array to copy that byte to</param>
        /// <param name="len">the number of bytes to copy</param>
        public void Put(byte[]? src, int srcOffset, int dstOffset, int len) {
            if (len == 0 || src == null)
                return;
            Assumes.NotNull(m_data);
            Buffer.BlockCopy(src, srcOffset, m_data, dstOffset, len);
        }

        /// <summary>
        /// Copy the given single byte to this Msg's Data buffer.
        /// </summary>
        /// <param name="b">the source byte to copy from</param>
        public void Put(byte b)
        {
            Assumes.NotNull(m_data);
            m_data[m_offset] = b;
        }

        /// <summary>
        /// Copy the given single byte to this Msg's Data buffer at the given array-index.
        /// </summary>
        /// <param name="b">the source byte to copy from</param>
        /// <param name="i">index within the internal Data array to copy that byte to</param>
        public void Put(byte b, int i)
        {
            Assumes.NotNull(m_data);
            m_data[m_offset + i] = b;
        }

        /// <summary>
        /// Write a string into msg at the specified index
        /// </summary>
        /// <param name="encoding">The encoding to use for the writing</param>
        /// <param name="str">The string to write</param>
        /// <param name="index">The index to write the string to</param>
        public void Put(Encoding encoding, string str, int index)
        {
            Assumes.NotNull(m_data);
            encoding.GetBytes(str, 0, str.Length, m_data, m_offset + index);
        }
        /// <summary>
        /// Copy the given byte-array data to this Msg's Data buffer.
        /// </summary>
        /// <param name="src">the source byte-array to copy from</param>
        /// <param name="offset">index within the internal Data array to copy that byte to</param>
        public void Put(Span<byte> src, int offset) 
        {
            src.CopyTo(Slice(offset));
        }
        
        /// <summary>
        /// Get and set the byte value in the <see cref="Data"/> buffer at a specific index.
        /// </summary>
        /// <param name="index">The index to access</param>
        /// <returns></returns>
        public byte this[int index]
        {
            get
            {
                Assumes.NotNull(m_data);
                return m_data[m_offset + index];
            }
            set
            {
                Assumes.NotNull(m_data);
                m_data[m_offset + index] = value;
            }
        }

        /// <summary>
        /// Close this Msg, and effectively make this Msg a copy of the given source Msg
        /// by simply setting it to point to the given source Msg.
        /// If this is a Pool Msg, then this also increases the reference-counter and sets the Shared bit.
        /// </summary>
        /// <param name="src">the source Msg to copy from</param>
        /// <exception cref="FaultException">The object is not initialised.</exception>
        public void Copy(ref Msg src)
        {
            // Check the validity of the source.
            if (!src.IsInitialised)
                throw new FaultException("Cannot copy an uninitialised Msg.");

            if (IsInitialised)
                Close();

            if (src.MsgType == MsgType.Pool)
            {
                Assumes.NotNull(src.m_refCount);
                
                // One reference is added to shared messages. Non-shared messages
                // are turned into shared messages and reference count is set to 2.
                if (IsShared)
                {
                    src.m_refCount.Increase(1);
                }
                else
                {
                    src.Flags |= MsgFlags.Shared;
                    src.m_refCount.Set(2);
                }
            }

            // Populate this instance via a memberwise-copy from the 'src' instance.
            this = src;
        }

        /// <summary>
        /// Increase Offset and decrease Size by the given count.
        /// </summary>
        /// <param name="count">Number of bytes to remove from a message</param>
        public void TrimPrefix(int count)
        {
            if (count > Size || count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count should be between 0 and size");

            m_offset = m_offset + count;
            Size = Size - count;
        }

        /// <summary>
        /// Close this Msg and make it reference the given source Msg, and then clear the Msg to empty.
        /// </summary>
        /// <param name="src">the source-Msg to become</param>
        /// <exception cref="FaultException">The object is not initialised.</exception>
        public void Move(ref Msg src)
        {
            // Check the validity of the source.
            if (!src.IsInitialised)
                throw new FaultException("Cannot move an uninitialised Msg.");

            if (IsInitialised)
                Close();

            this = src;

            src.InitEmpty();
        }

        /// <summary>
        /// Returns a new array containing the first <see cref="Size"/> bytes of <see cref="Data"/>.
        /// Deprecated: use <see cref="ToArray()"/>
        /// </summary>
        public byte[] CloneData()
        {
            var data = new byte[Size];

            if (Size > 0)
                Buffer.BlockCopy(m_data!, m_offset, data, 0, Size);

            return data;
        }

        private void EnsureAtomicCounterNull()
        {
            if(m_refCount != null)
            {
                AtomicCounterPool.Return(m_refCount);
                m_refCount = null;
            }
        }

        #region Internal unsafe methods
        
        internal byte[] UnsafeToArray()
        {
            Assumes.NotNull(m_data);

            if (m_offset == 0 && m_data.Length == Size)
                return m_data;
            return CloneData();
        }

        internal byte[]? UnsafeData => m_data;
        internal int UnsafeOffset => m_offset;

        #endregion
    }
}
