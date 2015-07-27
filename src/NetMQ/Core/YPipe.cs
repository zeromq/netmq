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

using System.Diagnostics;
using System.Threading;
using NetMQ.Core.Utils;

namespace NetMQ.Core
{
    internal sealed class YPipe<T>
    {
        /// <summary>
        /// Allocation-efficient queue to store pipe items.
        /// Front of the queue points to the first prefetched item, back of
        /// the pipe points to last un-flushed item. Front is used only by
        /// reader thread, while back is used only by writer thread.
        /// </summary>
        private readonly YQueue<T> m_queue;

        /// <summary>
        /// Points to the first un-flushed item. This variable is used
        /// exclusively by writer thread.
        /// </summary>
        private int m_flushFromIndex;

        /// <summary>
        /// Points to the first un-prefetched item. This variable is used
        /// exclusively by reader thread.
        /// </summary>
        private int m_readToIndex;

        /// <summary>
        /// Points to the first item to be flushed in the future.
        /// </summary>
        private int m_flushToIndex;

#if DEBUG
        private string m_name;
#endif

        /// <summary>
        /// The single point of contention between writer and reader thread.
        /// Points past the last flushed item. If it is NULL,
        /// reader is asleep. This pointer should be always accessed using
        /// atomic operations.
        /// </summary>
        private int m_lastAllowedToReadIndex;

        public YPipe(int qsize, string name)
        {
#if DEBUG
            m_name = name;
#endif
            m_queue = new YQueue<T>(qsize);
            m_lastAllowedToReadIndex = m_flushFromIndex = m_readToIndex = m_flushToIndex = m_queue.BackPos;
        }

        /// <summary>
        /// Write an item to the pipe.  Don't flush it yet. If incomplete is
        /// set to true the item is assumed to be continued by items
        /// subsequently written to the pipe. Incomplete items are never
        /// flushed down the stream.
        /// </summary>
        public void Write(ref T value, bool incomplete)
        {
            // Place the value to the queue, add new terminator element.
            m_queue.Push(ref value);

            // Move the "flush up to here" pointer.
            if (!incomplete)
            {
                m_flushToIndex = m_queue.BackPos;
            }
        }

        /// <summary>
        /// Pop an incomplete item from the pipe.
        /// </summary>
        /// <returns>the element revoked if such item exists, <c>null</c> otherwise.</returns>
        public bool Unwrite(ref T value)
        {
            if (m_flushToIndex == m_queue.BackPos)
                return false;
            value = m_queue.Unpush();

            return true;
        }

        /// <summary>
        /// Flush all the completed items into the pipe.
        /// </summary>
        /// <returns> Returns <c>false</c> if the reader thread is sleeping. In that case, caller is obliged to
        /// wake the reader up before using the pipe again.
        /// </returns>
        public bool Flush()
        {
            // If there are no un-flushed items, do nothing.
            if (m_flushFromIndex == m_flushToIndex)
            {
                return true;
            }

            // Try to set 'c' to 'flushToIndex'.
            if (Interlocked.CompareExchange(ref m_lastAllowedToReadIndex, m_flushToIndex, m_flushFromIndex) != m_flushFromIndex)
            {
                // Compare-and-swap was unsuccessful because 'lastAllowedToReadIndex' is NULL (-1).
                // This means that the reader is asleep. Therefore we don't
                // care about thread-safeness and update c in non-atomic
                // manner. We'll return false to let the caller know
                // that reader is sleeping.
                Interlocked.Exchange(ref m_lastAllowedToReadIndex, m_flushToIndex);
                m_flushFromIndex = m_flushToIndex;
                return false;
            }

            // Reader is alive. Nothing special to do now. Just move
            // the 'first un-flushed item' pointer to 'flushToIndex'.
            m_flushFromIndex = m_flushToIndex;
            return true;
        }

        /// <summary>
        /// Check whether item is available for reading.
        /// </summary>
        public bool CheckRead()
        {
            // Was the value prefetched already? If so, return.
            int head = m_queue.FrontPos;
            if (head != m_readToIndex && m_readToIndex != -1)
                return true;

            // There's no prefetched value, so let us prefetch more values.
            // Prefetching is to simply retrieve the
            // pointer from c in atomic fashion. If there are no
            // items to prefetch, set c to -1 (using compare-and-swap).
            if (Interlocked.CompareExchange(ref m_lastAllowedToReadIndex, -1, head) == head)
            {
                // nothing to read, h == r must be the same
            }
            else
            {
                // something to have been written
                m_readToIndex = m_lastAllowedToReadIndex;
            }

            // If there are no elements prefetched, exit.
            // During pipe's lifetime readToIndex should never be NULL, however,
            // it can happen during pipe shutdown when items
            // are being deallocated.
            if (head == m_readToIndex || m_readToIndex == -1)
                return false;

            // There was at least one value prefetched.
            return true;
        }


        /// <summary>
        /// Attempts to read an item from the pipe.
        /// </summary>
        /// <returns><c>true</c> if the read succeeded, otherwise <c>false</c>.</returns>
        public bool TryRead(out T value)
        {
            // Try to prefetch a value.
            if (!CheckRead())
            {
                value = default(T);
                return false;
            }

            // There was at least one value prefetched.
            // Return it to the caller.
            value = m_queue.Pop();
            return true;
        }

        /// <summary>
        /// Applies the function fn to the first element in the pipe
        /// and returns the value returned by the fn.
        /// The pipe mustn't be empty or the function crashes.
        /// </summary>
        public T Probe()
        {
            bool rc = CheckRead();
            Debug.Assert(rc);

            T value = m_queue.Front;
            return value;
        }
    }
}
