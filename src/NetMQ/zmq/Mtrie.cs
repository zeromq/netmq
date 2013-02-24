/*              
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011-2012 Spotify AB
    Copyright (c) 2011 Other contributors as noted in the AUTHORS file
                    
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
using System.Collections.Generic;
using System.Diagnostics;

//Multi-trie. Each node in the trie is a set of pointers to pipes.
namespace NetMQ.zmq
{
	public class Mtrie {
		private HashSet<Pipe> m_pipes;

		private int m_min;
		private int m_count;
		private int m_liveNodes;
		private Mtrie[] m_next;

		public delegate void MtrieDelegate(Pipe pipe, byte[] data, Object arg);
    
		public Mtrie() {
			m_min = 0;
			m_count = 0;
			m_liveNodes = 0;
        
			m_pipes = null;
			m_next = null;
		}
    
		public bool Add (byte[] prefix, Pipe pipe)
		{
			return AddHelper (prefix, 0, pipe);
		}

		//  Add key to the trie. Returns true if it's a new subscription
		//  rather than a duplicate.
		public bool Add (byte[] prefix, int start, Pipe pipe)
		{
			return AddHelper (prefix, start, pipe);
		}


		private bool AddHelper (byte[] prefix, int start, Pipe pipe)
		{
			//  We are at the node corresponding to the prefix. We are done.
			if (prefix == null || prefix.Length == start) {
				bool result = m_pipes == null;
				if (m_pipes == null)
					m_pipes = new HashSet<Pipe>();
				m_pipes.Add (pipe);
				return result;
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
					int oldc = m_min;
					Mtrie oldp = m_next[0];
					m_count = (m_min < c ? c - m_min : m_min - c) + 1;
					m_next = new Mtrie[m_count];
					m_min = Math.Min (m_min, c);
					m_next[oldc - m_min] = oldp;
				}
				else if (m_min < c) {

					//  The new character is above the current character range.
					m_count = c - m_min + 1;
					m_next = Realloc(m_next, m_count, true);
				}
				else {

					//  The new character is below the current character range.
					m_count = (m_min + m_count) - c;
					m_next = Realloc(m_next, m_count, false);
					m_min = c;
				}
			}

			//  If next node does not exist, create one.
			if (m_count == 1) {
				if (m_next == null) {
					m_next = new Mtrie[1];
					m_next[0] = new Mtrie();
					++m_liveNodes;
					//alloc_Debug.Assert(next.node);
				}
				return m_next[0].AddHelper (prefix, start + 1, pipe);
			}
			else {
				if (m_next[c - m_min] == null) {
					m_next[c - m_min] = new Mtrie();
					++m_liveNodes;
					//alloc_Debug.Assert(next.table [c - min]);
				}
				return m_next[c - m_min].AddHelper (prefix , start + 1, pipe);
			}
		}
    
		private Mtrie[] Realloc (Mtrie[] table, int size, bool ended) 
		{
			return Utils.Realloc(table, size, ended);
		}
    
		//  Remove all subscriptions for a specific peer from the trie.
		//  If there are no subscriptions left on some topics, invoke the
		//  supplied callback function.
		public bool RemoveHelper(Pipe pipe, MtrieDelegate func, Object arg)
		{
			return RemoveHelper(pipe, new byte[0], 0, 0, func, arg );
		}

		private bool RemoveHelper(Pipe pipe, byte[] buff, int buffsize, int maxbuffsize,
		                       MtrieDelegate func, Object arg) {
        
			//  Remove the subscription from this node.
			if (m_pipes != null && m_pipes.Remove(pipe) && m_pipes.Count == 0) {
				func(null, buff, arg);
				m_pipes = null;
			}

			//  Adjust the buffer.
			if (buffsize >= maxbuffsize) {
				maxbuffsize = buffsize + 256;
				buff = Utils.Realloc(buff, maxbuffsize);
			}

			//  If there are no subnodes in the trie, return.
			if (m_count == 0)
				return true;

			//  If there's one subnode (optimisation).
			if (m_count == 1) {
				buff[buffsize] = (byte) m_min;
				buffsize ++;
				m_next[0].RemoveHelper (pipe, buff, buffsize, maxbuffsize,
				                   func, arg);

				//  Prune the node if it was made redundant by the removal
				if (m_next[0].IsRedundant ) {
					m_next = null;
					m_count = 0;
					--m_liveNodes;
					Debug.Assert(m_liveNodes == 0);
				}
				return true;
			}

			//  If there are multiple subnodes.
			//
			//  New min non-null character in the node table after the removal
			int newMin = m_min + m_count - 1;
			//  New max non-null character in the node table after the removal
			int newMax = m_min;
			for (int c = 0; c != m_count; c++) {
				buff[buffsize] = (byte) (m_min + c);
				if (m_next[c] != null) {
					m_next[c].RemoveHelper (pipe, buff, buffsize + 1,
					                   maxbuffsize, func, arg);

					//  Prune redundant nodes from the mtrie
					if (m_next[c].IsRedundant ) {
						m_next[c] = null;

						Debug.Assert(m_liveNodes > 0);
						--m_liveNodes;
					}
					else {
						//  The node is not redundant, so it's a candidate for being
						//  the new min/max node.
						//
						//  We loop through the node array from left to right, so the
						//  first non-null, non-redundant node encountered is the new
						//  minimum index. Conversely, the last non-redundant, non-null
						//  node encountered is the new maximum index.
						if (c + m_min < newMin)
							newMin = c + m_min;
						if (c + m_min > newMax)
							newMax = c + m_min;
					}
				}
			}

			Debug.Assert(m_count > 1);

			//  Free the node table if it's no longer used.
			if (m_liveNodes == 0) {
				m_next = null;
				m_count = 0;
			}
				//  Compact the node table if possible
			else if (m_liveNodes == 1) {
				//  If there's only one live node in the table we can
				//  switch to using the more compact single-node
				//  representation
				Debug.Assert(newMin == newMax);
				Debug.Assert(newMin >= m_min && newMin < m_min + m_count);
				Mtrie node = m_next [newMin - m_min];
				Debug.Assert(node != null);
				m_next = null;
				m_next = new Mtrie[]{node};
				m_count = 1;
				m_min = newMin;
			}
			else if (m_liveNodes > 1 && (newMin > m_min || newMax < m_min + m_count - 1)) {
				Debug.Assert(newMax - newMin + 1 > 1);

				Mtrie[] old_table = m_next;
				Debug.Assert(newMin > m_min || newMax < m_min + m_count - 1);
				Debug.Assert(newMin >= m_min);
				Debug.Assert(newMax <= m_min + m_count - 1);
				Debug.Assert(newMax - newMin + 1 < m_count);
				m_count = newMax - newMin + 1;
				m_next = new Mtrie[m_count];

				Array.Copy(old_table, (newMin - m_min), m_next, 0, m_count);

				m_min = newMin;
			}
			return true;
		                       }

		//  Remove specific subscription from the trie. Return true is it was
		//  actually removed rather than de-duplicated.
		public bool Remove (byte[] prefix, int start, Pipe pipe)
		{
			return RemovemHelper (prefix, start,  pipe);
		}


		private bool RemovemHelper (byte[] prefix, int start, Pipe pipe)
		{
			if (prefix == null || prefix.Length == start) {
				if (m_pipes != null) {
					bool erased = m_pipes.Remove(pipe);
					Debug.Assert(erased);
					if (m_pipes.Count == 0) {
						m_pipes = null;
					}
				}
				return m_pipes == null;
			}

			byte c = prefix[ start ];
			if (m_count == 0 || c < m_min || c >= m_min + m_count)
				return false;

			Mtrie nextNode =
				m_count == 1 ? m_next[0] : m_next[c - m_min];

			if (nextNode == null)
				return false;

			bool ret = nextNode.RemovemHelper (prefix , start + 1, pipe);
			if (nextNode.IsRedundant ) {
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
						int i;
						for (i = 0; i < m_count; ++i) {
							if (m_next[i] != null) {
								break;
							}
						}

						Debug.Assert(i < m_count);
						m_min += i;
						m_count = 1;
						Mtrie old = m_next [i];
						m_next = new Mtrie [] { old };
					}
					else if (c == m_min) {
						//  We can compact the table "from the left"
						int i;
						for (i = 1; i < m_count; ++i) {
							if (m_next[i] != null) {
								break;
							}
						}

						Debug.Assert(i < m_count);
						m_min += i;
						m_count -= i;
						m_next = Realloc (m_next, m_count, false);
					}
					else if (c == m_min + m_count - 1) {
						//  We can compact the table "from the right"
						int i;
						for (i = 1; i < m_count; ++i) {
							if (m_next[m_count - 1 - i] != null) {
								break;
							}
						}
						Debug.Assert(i < m_count);
						m_count -= i;
						m_next = Realloc(m_next, m_count, true);
					}
				}
			}

			return ret;
		}

		//  Signal all the matching pipes.
		public void Match(byte[] data, int size, MtrieDelegate func, Object arg) {
			Mtrie current = this;
        
			int idx = 0;
        
			while (true) {
            
            
				//  Signal the pipes attached to this node.
				if (current.m_pipes != null) {
					foreach (Pipe it in current.m_pipes)
						func(it, null, arg);
				}

				//  If we are at the end of the message, there's nothing more to match.
				if (size == 0)
					break;

				//  If there are no subnodes in the trie, return.
				if (current.m_count == 0)
					break;

				byte c = data[idx];
				//  If there's one subnode (optimisation).
				if (current.m_count == 1) {
					if (c != current.m_min)
						break;
					current = current.m_next[0];
					idx++;
					size--;
					continue;
				}

				//  If there are multiple subnodes.
				if (c < current.m_min || c >=
				    current.m_min + current.m_count)
					break;
				if (current.m_next [c - current.m_min] == null)
					break;
				current = current.m_next [c - current.m_min];
				idx++;
				size--;
			}
		}

		private bool IsRedundant 
		{
			get { return m_pipes == null && m_liveNodes == 0; }
		}

	}
}
