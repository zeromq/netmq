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

using System;
using System.Diagnostics;

namespace NetMQ.zmq
{
	public class YQueue<T> where T : class
	{
		/// <summary> Individual memory chunk to hold N elements. </summary>
		private class Chunk
		{
			public Chunk(int size, int globalIndex)
			{
				Values = new T[size];
				GlobalPosition = new int[size];
				Debug.Assert(Values != null);
				Previous = Next = null;
				for (int i = 0; i != Values.Length; i++)
				{
					GlobalPosition[i] = globalIndex;
					globalIndex++;
				}
			}

			public T[] Values { get; private set; }
			/// <summary> Contains global index positions of elements in the chunk. </summary>
			public int[] GlobalPosition { get; private set; }
			public Chunk Previous { get; set; }
			public Chunk Next { get; set; }
		}

		//  Back position may point to invalid memory if the queue is empty,
		//  while begin & end positions are always valid. Begin position is
		//  accessed exclusively be queue reader (front/pop), while back and
		//  end positions are accessed exclusively by queue writer (back/push).
		private volatile Chunk m_beginChunk;
		private int m_beginPositionInChunk;
		private Chunk m_backChunk;
		private int m_backPositionInChunk;
		private Chunk m_endChunk;
		private int m_endPos;
		private Chunk m_spareChunk;
		private readonly int m_size;

		//  People are likely to produce and consume at similar rates.  In
		//  this scenario holding onto the most recently freed chunk saves
		//  us from having to call malloc/free.

		private int m_nextGlobalIndex;


		public YQueue(int size)
		{
			if (size<2)
				throw new ArgumentOutOfRangeException("size", "Size should be no less than 2");

			this.m_size = size;
			m_nextGlobalIndex = 0;
			m_beginChunk = new Chunk(size, m_nextGlobalIndex);
			m_nextGlobalIndex += size;
			m_beginPositionInChunk = 0;
			m_backPositionInChunk = 0;
			m_backChunk = m_beginChunk;
			m_spareChunk = m_beginChunk;
			m_endChunk = m_beginChunk;
			m_endPos = 1;
		}

		/// <summary> Gets the index of the front element of the queue. </summary>
		/// <value> The index of the front element of the queue. </value>
		/// <remarks> If the queue is empty, it should be equal to <see cref="BackPos"/>. </remarks>
		public int FrontPos { get { return m_beginChunk.GlobalPosition[m_beginPositionInChunk]; } }

		/// <summary> Gets the front element of the queue. If the queue is empty, behaviour is undefined. </summary>
		/// <value> The front element of the queue. </value>
		public T Front { get { return m_beginChunk.Values[m_beginPositionInChunk]; } }

		/// <summary> Gets the index of the back element of the queue. </summary>
		/// <value> The index of the back element of the queue. </value>
		/// <remarks> If the queue is empty, it should be equal to <see cref="FrontPos"/>. </remarks>
		public int BackPos { get { return m_backChunk.GlobalPosition[m_backPositionInChunk]; } }
		
		/// <summary> Retrieves the element at the front of the queue. </summary>
		/// <returns> The element taken from queue. </returns>
		public T Pop()
		{
			T value = m_beginChunk.Values[m_beginPositionInChunk];
			m_beginChunk.Values[m_beginPositionInChunk] = null;
			m_beginPositionInChunk++;
			if (m_beginPositionInChunk == m_size)
			{
				m_beginChunk = m_beginChunk.Next;
				m_beginChunk.Previous = null;
				m_beginPositionInChunk = 0;
			}
			return value;
		}


		/// <summary> Adds an element to the back end of the queue. </summary>
		/// <param name="val">The value to be pushed.</param>
		public void Push(T val)
		{
			m_backChunk.Values[m_backPositionInChunk] = val;
			m_backChunk = m_endChunk;
			m_backPositionInChunk = m_endPos;

			m_endPos++;
			if (m_endPos != m_size)
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
				m_endChunk.Next = new Chunk(m_size, m_nextGlobalIndex);
				m_nextGlobalIndex += m_size;
				m_endChunk.Next.Previous = m_endChunk;
			}
			m_endChunk = m_endChunk.Next;
			m_endPos = 0;
		}

		/// <summary> Removes element from the back end of the queue. In other words it rollbacks last push to the queue. </summary>
		/// <remarks> Caller is responsible for destroying the object being unpushed. 
		/// The caller must also guarantee that the queue isn't empty when unpush is called. 
		/// It cannot be done automatically as the read side of the queue can be managed by different, 
		/// completely unsynchronized thread.</remarks>
		public T Unpush()
		{
			//  First, move 'back' one position backwards.
			if (m_backPositionInChunk > 0)
				m_backPositionInChunk--;
			else
			{
				m_backPositionInChunk = m_size - 1;
				m_backChunk = m_backChunk.Previous;
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
				m_endChunk = m_endChunk.Previous;
				m_endChunk.Next = null;
			}

			//capturing and removing the unpushed value from chunk.
			T value = m_backChunk.Values[m_backPositionInChunk];
			m_backChunk.Values[m_backPositionInChunk] = null;
			return value;
		}
	}
}
