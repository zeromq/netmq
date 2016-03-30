/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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
using System.Diagnostics;
using JetBrains.Annotations;

namespace NetMQ.Core.Utils
{
    /// <summary>A FIFO queue.</summary>
    /// <remarks>
    /// The class supports:
    /// <list type="bullet">
    /// <item>Push-front via <see cref="Push"/>.</item>
    /// <item>Pop-back via <see cref="Pop"/>.</item>
    /// <item>Pop-front via <see cref="Unpush"/>.</item>
    /// </list>
    /// As such it is only one operation short of being a double-ended queue (dequeue or deque).
    /// <para/>
    /// The internal implementation consists of a doubly-linked list of fixed-size arrays.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    internal sealed class YQueue<T>
    {
        #region Nested class: Chunk

        /// <summary>Individual memory chunk to hold N elements.</summary>
        private sealed class Chunk
        {
            public Chunk(int size, int globalIndex)
            {
                Values = new T[size];
                GlobalOffset = globalIndex;
                Debug.Assert(Values != null);
            }

            [NotNull]
            public T[] Values { get; }

            /// <summary>Contains global index positions of elements in the chunk.</summary>
            public int GlobalOffset { get; }

            /// <summary>Optional link to the previous <see cref="Chunk"/>.</summary>
            [CanBeNull]
            public Chunk Previous { get; set; }

            /// <summary>Optional link to the next <see cref="Chunk"/>.</summary>
            [CanBeNull]
            public Chunk Next { get; set; }
        }

        #endregion

        private readonly int m_chunkSize;

        // Back position may point to invalid memory if the queue is empty,
        // while begin & end positions are always valid. Begin position is
        // accessed exclusively be queue reader (front/pop), while back and
        // end positions are accessed exclusively by queue writer (back/push).
        private volatile Chunk m_beginChunk;
        private int m_beginPositionInChunk;
        private Chunk m_backChunk;
        private int m_backPositionInChunk;
        private Chunk m_endChunk;
        private int m_endPosition;
        private Chunk m_spareChunk;

        // People are likely to produce and consume at similar rates.  In
        // this scenario holding onto the most recently freed chunk saves
        // us from having to call malloc/free.

        private int m_nextGlobalIndex;

        /// <param name="chunkSize">the size to give the new YQueue</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> should be no less than 2</exception>
        public YQueue(int chunkSize)
        {
            if (chunkSize < 2)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Should be no less than 2");

            m_chunkSize = chunkSize;

            m_beginChunk = new Chunk(m_chunkSize, 0);
            m_nextGlobalIndex = m_chunkSize;
            m_backChunk = m_beginChunk;
            m_spareChunk = m_beginChunk;
            m_endChunk = m_beginChunk;
            m_endPosition = 1;
        }

        /// <summary>Gets the index of the front element of the queue.</summary>
        /// <value>The index of the front element of the queue.</value>
        /// <remarks>If the queue is empty, it should be equal to <see cref="BackPos"/>.</remarks>
        public int FrontPos => m_beginChunk.GlobalOffset + m_beginPositionInChunk;

        /// <summary>Gets the front element of the queue. If the queue is empty, behaviour is undefined.</summary>
        /// <value>The front element of the queue.</value>
        public T Front => m_beginChunk.Values[m_beginPositionInChunk];

        /// <summary>Gets the index of the back element of the queue.</summary>
        /// <value>The index of the back element of the queue.</value>
        /// <remarks>If the queue is empty, it should be equal to <see cref="FrontPos"/>.</remarks>
        public int BackPos => m_backChunk.GlobalOffset + m_backPositionInChunk;

        /// <summary>Retrieves the element at the front of the queue.</summary>
        /// <returns>The element taken from queue.</returns>
        public T Pop()
        {
            T value = m_beginChunk.Values[m_beginPositionInChunk];
            m_beginChunk.Values[m_beginPositionInChunk] = default(T);

            m_beginPositionInChunk++;
            if (m_beginPositionInChunk == m_chunkSize)
            {
                m_beginChunk = m_beginChunk.Next;
                m_beginChunk.Previous = null;
                m_beginPositionInChunk = 0;
            }
            return value;
        }

        /// <summary>Adds an element to the back end of the queue.</summary>
        /// <param name="val">The value to be pushed.</param>
        public void Push(ref T val)
        {
            m_backChunk.Values[m_backPositionInChunk] = val;
            m_backChunk = m_endChunk;
            m_backPositionInChunk = m_endPosition;

            m_endPosition++;
            if (m_endPosition != m_chunkSize)
                return;

            Chunk sc = m_spareChunk;
            if (sc != m_beginChunk)
            {
                m_spareChunk = m_spareChunk.Next;
                m_endChunk.Next = sc;
                sc.Previous = m_endChunk;
            }
            else
            {
                m_endChunk.Next = new Chunk(m_chunkSize, m_nextGlobalIndex);
                m_nextGlobalIndex += m_chunkSize;
                m_endChunk.Next.Previous = m_endChunk;
            }
            m_endChunk = m_endChunk.Next;
            m_endPosition = 0;
        }

        /// <summary>Removes element from the back end of the queue, rolling back the last call to <see cref="Push"/>.</summary>
        /// <remarks>The caller must guarantee that the queue isn't empty when calling this method.
        /// It cannot be done automatically as the read side of the queue can be managed by different,
        /// completely unsynchronized threads.</remarks>
        /// <returns>The last item passed to <see cref="Push"/>.</returns>
        public T Unpush()
        {
            // First, move 'back' one position backwards.
            if (m_backPositionInChunk > 0)
            {
                m_backPositionInChunk--;
            }
            else
            {
                m_backPositionInChunk = m_chunkSize - 1;
                m_backChunk = m_backChunk.Previous;
            }

            // Now, move 'end' position backwards. Note that obsolete end chunk
            // is not used as a spare chunk. The analysis shows that doing so
            // would require free and atomic operation per chunk deallocated
            // instead of a simple free.
            if (m_endPosition > 0)
            {
                m_endPosition--;
            }
            else
            {
                m_endPosition = m_chunkSize - 1;
                m_endChunk = m_endChunk.Previous;
                m_endChunk.Next = null;
            }

            // Capturing and removing the unpushed value from chunk.
            T value = m_backChunk.Values[m_backPositionInChunk];
            m_backChunk.Values[m_backPositionInChunk] = default(T);

            return value;
        }
    }
}
