/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011-2012 Spotify AB
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
	public class Trie {
		private int m_refcnt;

		private byte m_min;
		private short m_count;
		private short m_liveNodes;
    
		public delegate void TrieDelegate(byte[] data, int size, Object arg);
		Trie[] m_next;
    
		public Trie() {
			m_min = 0;
			m_count = 0;
			m_liveNodes = 0;
        
			m_refcnt = 0;
			m_next = null;
		}
    
		//  Add key to the trie. Returns true if this is a new item in the trie
		//  rather than a duplicate.
		public bool Add (byte[] prefix)
		{
			return Add (prefix, 0);
		}
    
		public bool Add (byte[] prefix, int start)
		{
			//  We are at the node corresponding to the prefix. We are done.
			if (prefix == null || prefix.Length == start) {
				++m_refcnt;
				return m_refcnt == 1;
			}

			byte c = prefix[start];
			if (c < m_min || c >= m_min + m_count) {

				//  The character is out of range of currently handled
				//  charcters. We have to extend the table.
				if (m_count == 0) {
					m_min = c;
					m_count = 1;
					m_next = null;
				}
				else if (m_count == 1) {
					byte oldc = m_min;
					Trie oldp = m_next[0];
					m_count = (short) ((m_min < c ? c - m_min : m_min - c) + 1);
					m_next = new Trie[m_count];
					m_min = Math.Min (m_min, c);
					m_next[oldc - m_min] = oldp;
				}
				else if (m_min < c) {

					//  The new character is above the current character range.
					m_count = (short) (c - m_min + 1);
					m_next = realloc(m_next, m_count, true);
				}
				else {

					//  The new character is below the current character range.
					m_count = (short) ((m_min + m_count) - c);
					m_next = realloc(m_next, m_count, false);
					m_min = c;
				}
			}

			//  If next node does not exist, create one.
			if (m_count == 1) {
				if (m_next == null) {
					m_next = new Trie[1];
					m_next[0] = new Trie();
					++m_liveNodes;
					//alloc_Debug.Assert(next.node);
				}
				return m_next[0].Add (prefix, start + 1);
			}
			else {
				if (m_next[c - m_min] == null) {
					m_next[c - m_min] = new Trie();
					++m_liveNodes;
					//alloc_Debug.Assert(next.table [c - min]);
				}
				return m_next[c - m_min].Add (prefix , start + 1);
			}
		}
    
		private Trie[] realloc(Trie[] table, short size, bool ended) {
			return Utils.Realloc(table, size, ended);
		}
    
		//  Remove key from the trie. Returns true if the item is actually
		//  removed from the trie.
		public bool Remove (byte[] prefix, int start)
		{
			if (prefix == null || prefix.Length == start) {
				if (m_refcnt == 0)
					return false;
				m_refcnt--;
				return m_refcnt == 0;
			}

			byte c = prefix[ start ];
			if (m_count == 0 || c < m_min || c >= m_min + m_count)
				return false;

			Trie nextNode =
				m_count == 1 ? m_next[0] : m_next[c - m_min];

			if (nextNode == null)
				return false;

			bool ret = nextNode.Remove (prefix , start + 1);
			if (nextNode.IsRedundant ()) {
				//delete next_node;
				Debug.Assert(m_count > 0);

				if (m_count == 1) {
					m_next = null;
					m_count = 0;
					--m_liveNodes;
					Debug.Assert(m_liveNodes == 0);
				}
				else {
					m_next[c - m_min] = null ;
					Debug.Assert(m_liveNodes > 1);
					--m_liveNodes;

					//  Compact the table if possible
					if (m_liveNodes == 1) {
						//  If there's only one live node in the table we can
						//  switch to using the more compact single-node
						//  representation
						Trie node = null;
						for (short i = 0; i < m_count; ++i) {
							if (m_next[i] != null) {
								node = m_next[i];
								m_min = (byte)(i + m_min);
								break;
							}
						}

						Debug.Assert(node != null);
						//free (next.table);
						m_next = null;
						m_next = new Trie[]{node};
						m_count = 1;
					}
					else if (c == m_min) {
						//  We can compact the table "from the left"
						byte newMin = m_min;
						for (short i = 1; i < m_count; ++i) {
							if (m_next[i] != null) {
								newMin = (byte) (i + m_min);
								break;
							}
						}
						Debug.Assert(newMin != m_min);

						Debug.Assert(newMin > m_min);
						Debug.Assert(m_count > newMin - m_min);
						m_count = (short) (m_count - (newMin - m_min));
                    
						m_next = realloc(m_next, m_count, true);

						m_min = newMin;
					}
					else if (c == m_min + m_count - 1) {
						//  We can compact the table "from the right"
						short newCount = m_count;
						for (short i = 1; i < m_count; ++i) {
							if (m_next[m_count - 1 - i] != null) {
								newCount = (short) (m_count - i);
								break;
							}
						}
						Debug.Assert(newCount != m_count);
						m_count = newCount;

						m_next = realloc(m_next, m_count, false);
					}
				}
			}

			return ret;
		}
    
		//  Check whether particular key is in the trie.
		public bool Check (byte[] data)
		{
			//  This function is on critical path. It deliberately doesn't use
			//  recursion to get a bit better performance.
			Trie current = this;
			int start = 0;
			while (true) {

				//  We've found a corresponding subscription!
				if (current.m_refcnt > 0)
					return true;

				//  We've checked all the data and haven't found matching subscription.
				if (data.Length == start)
					return false;

				//  If there's no corresponding slot for the first character
				//  of the prefix, the message does not match.
				byte c = data[start];
				if (c < current.m_min || c >= current.m_min + current.m_count)
					return false;

				//  Move to the next character.
				if (current.m_count == 1)
					current = current.m_next[0];
				else {
					current = current.m_next[c - current.m_min];
					if (current == null)
						return false;
				}
				start++;
			}
		}
    
		//  Apply the function supplied to each subscription in the trie.
		public void Apply(TrieDelegate func, Object arg) {
			ApplyHelper(null, 0, 0, func, arg );
		}

		private void ApplyHelper(byte[] buff, int buffsize, int maxbuffsize, TrieDelegate func,
		                          Object arg) {
			//  If this node is a subscription, apply the function.
			if (m_refcnt > 0)
				func (buff, buffsize, arg);

			//  Adjust the buffer.
			if (buffsize  >= maxbuffsize) {
				maxbuffsize = buffsize  + 256;
				buff = Utils.Realloc (buff, maxbuffsize);
				Debug.Assert(buff!=null);
			}

			//  If there are no subnodes in the trie, return.
			if (m_count == 0)
				return;

			//  If there's one subnode (optimisation).
			if (m_count == 1) {
				buff [buffsize] = m_min;
				buffsize++;
				m_next[0].ApplyHelper (buff, buffsize, maxbuffsize, func, arg);
				return;
			}
        
			//  If there are multiple subnodes.
			for (short c = 0; c != m_count; c++) {
				buff [buffsize] = (byte) (m_min + c);
				if (m_next[c] != null)
					m_next[c].ApplyHelper (buff, buffsize + 1, maxbuffsize,
					                      func, arg);
			}
		                          }


		private bool IsRedundant ()
		{
			return m_refcnt == 0 && m_liveNodes == 0;
		}




	}
}
