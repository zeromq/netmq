/*  
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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

using System.Diagnostics;
using System.Threading;

namespace NetMQ.zmq
{
	public class YPipe<T> where T : class
	{

		//  Allocation-efficient queue to store pipe items.
		//  Front of the queue points to the first prefetched item, back of
		//  the pipe points to last un-flushed item. Front is used only by
		//  reader thread, while back is used only by writer thread.
		private readonly YQueue<T> m_queue;

		//  Points to the first un-flushed item. This variable is used
		//  exclusively by writer thread.
		private int m_w;

		//  Points to the first un-prefetched item. This variable is used
		//  exclusively by reader thread.
		private int m_r;

		//  Points to the first item to be flushed in the future.
		private int m_f;

		private string m_name;

		//  The single point of contention between writer and reader thread.
		//  Points past the last flushed item. If it is NULL,
		//  reader is asleep. This pointer should be always accessed using
		//  atomic operations.
		private int m_c;

		public YPipe(int qsize, string name)
		{
			m_name = name;
			m_queue = new YQueue<T>(qsize);
			m_c = m_w = m_r = m_f = m_queue.BackPos;
		}

		//  Write an item to the pipe.  Don't flush it yet. If incomplete is
		//  set to true the item is assumed to be continued by items
		//  subsequently written to the pipe. Incomplete items are never
		//  flushed down the stream.
		public void Write(T value, bool incomplete)
		{
			//  Place the value to the queue, add new terminator element.
			m_queue.Push(value);

			//  Move the "flush up to here" poiter.
			if (!incomplete)
			{
				m_f = m_queue.BackPos;
			}
		}

		//  Pop an incomplete item from the pipe. Returns true is such
		//  item exists, false otherwise.
		public T Unwrite()
		{

			if (m_f == m_queue.BackPos)
				return null;
			m_queue.Unpush();
			return m_queue.Back;
		}

		//  Flush all the completed items into the pipe. Returns false if
		//  the reader thread is sleeping. In that case, caller is obliged to
		//  wake the reader up before using the pipe again.
		public bool Flush()
		{
			//  If there are no un-flushed items, do nothing.
			if (m_w == m_f)
			{
				return true;
			}

			//  Try to set 'c' to 'f'.
			if (Interlocked.CompareExchange(ref m_c, m_f, m_w) != m_w)
			{
				//  Compare-and-swap was unsuccessful because 'c' is NULL.
				//  This means that the reader is asleep. Therefore we don't
				//  care about thread-safeness and update c in non-atomic
				//  manner. We'll return false to let the caller know
				//  that reader is sleeping.
				Interlocked.Exchange(ref m_c, m_f);
				m_w = m_f;
				return false;
			}

			//  Reader is alive. Nothing special to do now. Just move
			//  the 'first un-flushed item' pointer to 'f'.
			m_w = m_f;
			return true;
		}

		//  Check whether item is available for reading.
		public bool CheckRead()
		{
			//  Was the value prefetched already? If so, return.
			int h = m_queue.FrontPos;
			if (h != m_r) 
				return true;

			//  There's no prefetched value, so let us prefetch more values.
			//  Prefetching is to simply retrieve the
			//  pointer from c in atomic fashion. If there are no
			//  items to prefetch, set c to -1 (using compare-and-swap).
			if (Interlocked.CompareExchange(ref m_c, -1, h)==h) {
				// nothing to read, h == r must be the same
			} else {
				// something to have been written
				m_r = m_c;
			}
        
			//  If there are no elements prefetched, exit.
			//  During pipe's lifetime r should never be NULL, however,
			//  it can happen during pipe shutdown when items
			//  are being deallocated.
			if (h == m_r || m_r == -1) 
				return false;

			//  There was at least one value prefetched.
			return true;
		}


		//  Reads an item from the pipe. Returns false if there is no value.
		//  available.
		public T Read()
		{
			//  Try to prefetch a value.
			if (!CheckRead())
				return null;

			//  There was at least one value prefetched.
			//  Return it to the caller.
			T value = m_queue.Pop();

			return value;
		}

		//  Applies the function fn to the first elemenent in the pipe
		//  and returns the value returned by the fn.
		//  The pipe mustn't be empty or the function crashes.
		public T Probe()
		{
			bool rc = CheckRead();
			Debug.Assert(rc);

			T value = m_queue.Front;
			return value;
		}
	}
}
