/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011-2012 Spotify AB
    Copyright (c) 2011-2015 Other contributors as noted in the AUTHORS file

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
using JetBrains.Annotations;

namespace NetMQ.Core.Patterns.Utils
{
    /// <summary>
    /// Multi-trie. Each node in the trie is a set of pointers to pipes.
    /// </summary>
    internal class MultiTrie
    {
        private HashSet<Pipe> m_pipes;

        private int m_minCharacter;
        private int m_count;
        private int m_liveNodes;
        private MultiTrie[] m_next;

        public delegate void MultiTrieDelegate([CanBeNull] Pipe pipe, [CanBeNull] byte[] data, int size, [CanBeNull] object arg);

        public MultiTrie()
        {
            m_minCharacter = 0;
            m_count = 0;
            m_liveNodes = 0;

            m_pipes = null;
            m_next = null;
        }

        /// <summary>
        /// Add key to the trie. Returns true if it's a new subscription
        /// rather than a duplicate.
        /// </summary>
        public bool Add([CanBeNull] byte[] prefix, int start, int size, [NotNull] Pipe pipe)
        {
            return AddHelper(prefix, start, size, pipe);
        }

        private bool AddHelper([CanBeNull] byte[] prefix, int start, int size, [NotNull] Pipe pipe)
        {
            // We are at the node corresponding to the prefix. We are done.
            if (size == 0)
            {
                bool result = m_pipes == null;

                if (m_pipes == null)
                    m_pipes = new HashSet<Pipe>();

                m_pipes.Add(pipe);
                return result;
            }

            Debug.Assert(prefix != null);

            byte currentCharacter = prefix[start];

            if (currentCharacter < m_minCharacter || currentCharacter >= m_minCharacter + m_count)
            {
                // The character is out of range of currently handled
                // characters. We have to extend the table.
                if (m_count == 0)
                {
                    m_minCharacter = currentCharacter;
                    m_count = 1;
                    m_next = null;
                }
                else if (m_count == 1)
                {
                    int oldc = m_minCharacter;
                    MultiTrie oldp = m_next[0];
                    m_count = (m_minCharacter < currentCharacter ? currentCharacter - m_minCharacter : m_minCharacter - currentCharacter) + 1;
                    m_next = new MultiTrie[m_count];
                    m_minCharacter = Math.Min(m_minCharacter, currentCharacter);
                    m_next[oldc - m_minCharacter] = oldp;
                }
                else if (m_minCharacter < currentCharacter)
                {
                    // The new character is above the current character range.
                    m_count = currentCharacter - m_minCharacter + 1;
                    m_next = m_next.Resize(m_count, true);
                }
                else
                {
                    // The new character is below the current character range.
                    m_count = (m_minCharacter + m_count) - currentCharacter;
                    m_next = m_next.Resize(m_count, false);
                    m_minCharacter = currentCharacter;
                }
            }

            // If next node does not exist, create one.
            if (m_count == 1)
            {
                if (m_next == null)
                {
                    m_next = new MultiTrie[1];
                    m_next[0] = new MultiTrie();
                    ++m_liveNodes;
                }

                return m_next[0].AddHelper(prefix, start + 1, size - 1, pipe);
            }
            else
            {
                if (m_next[currentCharacter - m_minCharacter] == null)
                {
                    m_next[currentCharacter - m_minCharacter] = new MultiTrie();
                    ++m_liveNodes;
                }

                return m_next[currentCharacter - m_minCharacter].AddHelper(prefix, start + 1, size - 1, pipe);
            }
        }


        /// <summary>
        /// Remove all subscriptions for a specific peer from the trie.
        /// If there are no subscriptions left on some topics, invoke the
        /// supplied callback function.
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="func"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        public bool RemoveHelper([NotNull] Pipe pipe, [NotNull] MultiTrieDelegate func, [CanBeNull] object arg)
        {
            return RemoveHelper(pipe, EmptyArray<byte>.Instance, 0, 0, func, arg);
        }

        private bool RemoveHelper([NotNull] Pipe pipe, [NotNull] byte[] buffer, int bufferSize, int maxBufferSize, [NotNull] MultiTrieDelegate func, [CanBeNull] object arg)
        {
            // Remove the subscription from this node.
            if (m_pipes != null && m_pipes.Remove(pipe) && m_pipes.Count == 0)
            {
                func(pipe, buffer, bufferSize, arg);
                m_pipes = null;
            }

            // Adjust the buffer.
            if (bufferSize >= maxBufferSize)
            {
                maxBufferSize = bufferSize + 256;
                Array.Resize(ref buffer, maxBufferSize);
            }

            // If there are no subnodes in the trie, return.
            if (m_count == 0)
                return true;

            // If there's one subnode (optimisation).
            if (m_count == 1)
            {
                buffer[bufferSize] = (byte)m_minCharacter;
                bufferSize++;
                m_next[0].RemoveHelper(pipe, buffer, bufferSize, maxBufferSize, func, arg);

                // Prune the node if it was made redundant by the removal
                if (m_next[0].IsRedundant)
                {
                    m_next = null;
                    m_count = 0;
                    --m_liveNodes;
                    Debug.Assert(m_liveNodes == 0);
                }
                return true;
            }

            // If there are multiple subnodes.

            // New min non-null character in the node table after the removal
            int newMin = m_minCharacter + m_count - 1;

            // New max non-null character in the node table after the removal
            int newMax = m_minCharacter;

            for (int currentCharacter = 0; currentCharacter != m_count; currentCharacter++)
            {
                buffer[bufferSize] = (byte)(m_minCharacter + currentCharacter);
                if (m_next[currentCharacter] != null)
                {
                    m_next[currentCharacter].RemoveHelper(pipe, buffer, bufferSize + 1,
                        maxBufferSize, func, arg);

                    // Prune redundant nodes from the mtrie
                    if (m_next[currentCharacter].IsRedundant)
                    {
                        m_next[currentCharacter] = null;

                        Debug.Assert(m_liveNodes > 0);
                        --m_liveNodes;
                    }
                    else
                    {
                        // The node is not redundant, so it's a candidate for being
                        // the new min/max node.
                        //
                        // We loop through the node array from left to right, so the
                        // first non-null, non-redundant node encountered is the new
                        // minimum index. Conversely, the last non-redundant, non-null
                        // node encountered is the new maximum index.
                        if (currentCharacter + m_minCharacter < newMin)
                            newMin = currentCharacter + m_minCharacter;

                        if (currentCharacter + m_minCharacter > newMax)
                            newMax = currentCharacter + m_minCharacter;
                    }
                }
            }

            Debug.Assert(m_count > 1);

            // Free the node table if it's no longer used.
            if (m_liveNodes == 0)
            {
                m_next = null;
                m_count = 0;
            }
            // Compact the node table if possible
            else if (m_liveNodes == 1)
            {
                // If there's only one live node in the table we can
                // switch to using the more compact single-node
                // representation
                Debug.Assert(newMin == newMax);
                Debug.Assert(newMin >= m_minCharacter && newMin < m_minCharacter + m_count);

                MultiTrie node = m_next[newMin - m_minCharacter];

                Debug.Assert(node != null);

                m_next = null;
                m_next = new[] { node };
                m_count = 1;
                m_minCharacter = newMin;
            }
            else if (m_liveNodes > 1 && (newMin > m_minCharacter || newMax < m_minCharacter + m_count - 1))
            {
                Debug.Assert(newMax - newMin + 1 > 1);

                MultiTrie[] oldTable = m_next;
                Debug.Assert(newMin > m_minCharacter || newMax < m_minCharacter + m_count - 1);
                Debug.Assert(newMin >= m_minCharacter);
                Debug.Assert(newMax <= m_minCharacter + m_count - 1);
                Debug.Assert(newMax - newMin + 1 < m_count);
                m_count = newMax - newMin + 1;
                m_next = new MultiTrie[m_count];

                Array.Copy(oldTable, (newMin - m_minCharacter), m_next, 0, m_count);

                m_minCharacter = newMin;
            }
            return true;
        }

        /// <summary>
        /// Remove specific subscription from the trie. Return true is it was
        /// actually removed rather than de-duplicated.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="start"></param>
        /// <param name="size"></param>
        /// <param name="pipe"></param>
        /// <returns></returns>
        public bool Remove([NotNull] byte[] prefix, int start, int size, [NotNull] Pipe pipe)
        {
            return RemoveHelper(prefix, start, size, pipe);
        }

        private bool RemoveHelper([NotNull] byte[] prefix, int start, int size, [NotNull] Pipe pipe)
        {
            if (size == 0)
            {
                if (m_pipes != null)
                {
                    bool erased = m_pipes.Remove(pipe);
                    Debug.Assert(erased);
                    if (m_pipes.Count == 0)
                    {
                        m_pipes = null;
                    }
                }
                return m_pipes == null;
            }

            byte currentCharacter = prefix[start];
            if (m_count == 0 || currentCharacter < m_minCharacter || currentCharacter >= m_minCharacter + m_count)
                return false;

            MultiTrie nextNode = m_count == 1 ? m_next[0] : m_next[currentCharacter - m_minCharacter];

            if (nextNode == null)
                return false;

            bool ret = nextNode.RemoveHelper(prefix, start + 1, size - 1, pipe);
            if (nextNode.IsRedundant)
            {
                Debug.Assert(m_count > 0);

                if (m_count == 1)
                {
                    m_next = null;
                    m_count = 0;
                    --m_liveNodes;
                    Debug.Assert(m_liveNodes == 0);
                }
                else
                {
                    m_next[currentCharacter - m_minCharacter] = null;
                    Debug.Assert(m_liveNodes > 1);
                    --m_liveNodes;

                    // Compact the table if possible
                    if (m_liveNodes == 1)
                    {
                        // If there's only one live node in the table we can
                        // switch to using the more compact single-node
                        // representation
                        int i;
                        for (i = 0; i < m_count; ++i)
                        {
                            if (m_next[i] != null)
                            {
                                break;
                            }
                        }

                        Debug.Assert(i < m_count);
                        m_minCharacter += i;
                        m_count = 1;
                        MultiTrie old = m_next[i];
                        m_next = new[] { old };
                    }
                    else if (currentCharacter == m_minCharacter)
                    {
                        // We can compact the table "from the left"
                        int i;
                        for (i = 1; i < m_count; ++i)
                        {
                            if (m_next[i] != null)
                            {
                                break;
                            }
                        }

                        Debug.Assert(i < m_count);
                        m_minCharacter += i;
                        m_count -= i;
                        m_next = m_next.Resize(m_count, false);
                    }
                    else if (currentCharacter == m_minCharacter + m_count - 1)
                    {
                        // We can compact the table "from the right"
                        int i;
                        for (i = 1; i < m_count; ++i)
                        {
                            if (m_next[m_count - 1 - i] != null)
                            {
                                break;
                            }
                        }
                        Debug.Assert(i < m_count);
                        m_count -= i;
                        m_next = m_next.Resize(m_count, true);
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Signal all the matching pipes.
        /// </summary>
        public void Match([NotNull] byte[] data, int offset, int size, [NotNull] MultiTrieDelegate func, [CanBeNull] object arg)
        {
            MultiTrie current = this;

            int index = offset;

            while (true)
            {
                // Signal the pipes attached to this node.
                if (current.m_pipes != null)
                {
                    foreach (Pipe it in current.m_pipes)
                        func(it, null, 0, arg);
                }

                // If we are at the end of the message, there's nothing more to match.
                if (size == 0)
                    break;

                // If there are no subnodes in the trie, return.
                if (current.m_count == 0)
                    break;

                byte c = data[index];
                // If there's one subnode (optimisation).
                if (current.m_count == 1)
                {
                    if (c != current.m_minCharacter)
                        break;
                    current = current.m_next[0];
                    index++;
                    size--;
                    continue;
                }

                // If there are multiple subnodes.
                if (c < current.m_minCharacter || c >=
                    current.m_minCharacter + current.m_count)
                    break;
                if (current.m_next[c - current.m_minCharacter] == null)
                    break;
                current = current.m_next[c - current.m_minCharacter];
                index++;
                size--;
            }
        }

        private bool IsRedundant => m_pipes == null && m_liveNodes == 0;
    }
}
