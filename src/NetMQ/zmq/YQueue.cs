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

namespace NetMQ.zmq
{
	public class YQueue<T> where T : class
	{

		//  Individual memory chunk to hold N elements.
		private class Chunk
		{
			public Chunk(int size, int memoryPtr)
			{
				Values = new T[size];
				Pos = new int[size];
				Debug.Assert(Values != null);
				Prev = Next = null;
				for (int i = 0; i != Values.Length; i++)
				{
					Pos[i] = memoryPtr;
					memoryPtr++;
				}

			}

			public T[] Values { get; private set; }
			public int[] Pos { get; private set; }
			public Chunk Prev { get; set; }
			public Chunk Next { get; set; }
		}

		//  Back position may point to invalid memory if the queue is empty,
		//  while begin & end positions are always valid. Begin position is
		//  accessed exclusively be queue reader (front/pop), while back and
		//  end positions are accessed exclusively by queue writer (back/push).
		private volatile Chunk m_beginChunk;
		private int m_beginPos;
		private Chunk m_backChunk;
		private int m_backPos;
		private Chunk m_endChunk;
		private int m_endPos;
		private Chunk m_spareChunk;
		private readonly int m_size;

		//  People are likely to produce and consume at similar rates.  In
		//  this scenario holding onto the most recently freed chunk saves
		//  us from having to call malloc/free.

		private int m_memoryPtr;


		public YQueue(int size)
		{

			this.m_size = size;
			m_memoryPtr = 0;
			m_beginChunk = new Chunk(size, m_memoryPtr);
			m_memoryPtr += size;
			m_beginPos = 0;
			m_backPos = 0;
			m_backChunk = m_beginChunk;
			m_spareChunk = m_beginChunk;
			m_endChunk = m_beginChunk;
			m_endPos = 1;
		}

		public int FrontPos
		{
			get
			{
				return m_beginChunk.Pos[m_beginPos];
			}
		}

		//  Returns reference to the front element of the queue.
		//  If the queue is empty, behaviour is undefined.
		public T Front
		{
			get { return m_beginChunk.Values[m_beginPos]; }
		}

		public int BackPos
		{
			get { return m_backChunk.Pos[m_backPos]; }
		}

		//  Returns reference to the back element of the queue.
		//  If the queue is empty, behaviour is undefined.
		public T Back
		{
			get
			{
				return m_backChunk.Values[m_backPos];
			}
		}

		public T Pop()
		{
			T val = m_beginChunk.Values[m_beginPos];
			m_beginChunk.Values[m_beginPos] = null;
			m_beginPos++;
			if (m_beginPos == m_size)
			{
				m_beginChunk = m_beginChunk.Next;
				m_beginChunk.Prev = null;
				m_beginPos = 0;
			}
			return val;
		}

		//  Adds an element to the back end of the queue.
		public void Push(T val)
		{
			m_backChunk.Values[m_backPos] = val;
			m_backChunk = m_endChunk;
			m_backPos = m_endPos;

			m_endPos++;
			if (m_endPos != m_size)
				return;

			Chunk sc = m_spareChunk;
			if (sc != m_beginChunk)
			{
				m_spareChunk = m_spareChunk.Next;
				m_endChunk.Next = sc;
				sc.Prev = m_endChunk;
			}
			else
			{
				m_endChunk.Next = new Chunk(m_size, m_memoryPtr);
				m_memoryPtr += m_size;
				m_endChunk.Next.Prev = m_endChunk;
			}
			m_endChunk = m_endChunk.Next;
			m_endPos = 0;
		}

		//  Removes element from the back end of the queue. In other words
		//  it rollbacks last push to the queue. Take care: Caller is
		//  responsible for destroying the object being unpushed.
		//  The caller must also guarantee that the queue isn't empty when
		//  unpush is called. It cannot be done automatically as the read
		//  side of the queue can be managed by different, completely
		//  unsynchronised thread.
		public void Unpush()
		{
			//  First, move 'back' one position backwards.
			if (m_backPos > 0)
				m_backPos--;
			else
			{
				m_backPos = m_size - 1;
				m_backChunk = m_backChunk.Prev;
			}

			//  Now, move 'end' position backwards. Note that obsolete end chunk
			//  is not used as a spare chunk. The analysis shows that doing so
			//  would require free and atomic operation per chunk deallocated
			//  instead of a simple free.
			if (m_endPos > 0)
				m_endPos--;
			else
			{
				m_endPos = m_size - 1;
				m_endChunk = m_endChunk.Prev;
				m_endChunk.Next = null;
			}
		}



	}
}
