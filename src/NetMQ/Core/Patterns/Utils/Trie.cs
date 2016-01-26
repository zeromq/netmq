/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011-2012 Spotify AB
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

namespace NetMQ.Core.Patterns.Utils
{
    internal class Trie
    {
        private int m_referenceCount;

        private byte m_minCharacter;
        private short m_count;
        private short m_liveNodes;

        public delegate void TrieDelegate([NotNull] byte[] data, int size, [CanBeNull] object arg);

        private Trie[] m_next;

        /// <summary>
        /// Add key to the trie. Returns true if this is a new item in the trie
        /// rather than a duplicate.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="start"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool Add([NotNull] byte[] prefix, int start, int size)
        {
            // We are at the node corresponding to the prefix. We are done.
            if (size == 0)
            {
                ++m_referenceCount;
                return m_referenceCount == 1;
            }

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
                    byte oldc = m_minCharacter;
                    Trie oldp = m_next[0];
                    m_count = (short)((m_minCharacter < currentCharacter ? currentCharacter - m_minCharacter : m_minCharacter - currentCharacter) + 1);
                    m_next = new Trie[m_count];
                    m_minCharacter = Math.Min(m_minCharacter, currentCharacter);
                    m_next[oldc - m_minCharacter] = oldp;
                }
                else if (m_minCharacter < currentCharacter)
                {
                    // The new character is above the current character range.
                    m_count = (short)(currentCharacter - m_minCharacter + 1);
                    m_next = m_next.Resize(m_count, true);
                }
                else
                {
                    // The new character is below the current character range.
                    m_count = (short)((m_minCharacter + m_count) - currentCharacter);
                    m_next = m_next.Resize(m_count, false);
                    m_minCharacter = currentCharacter;
                }
            }

            // If next node does not exist, create one.
            if (m_count == 1)
            {
                if (m_next == null)
                {
                    m_next = new Trie[1];
                    m_next[0] = new Trie();
                    ++m_liveNodes;
                    //alloc_Debug.Assert(next.node);
                }
                return m_next[0].Add(prefix, start + 1, size - 1);
            }
            else
            {
                if (m_next[currentCharacter - m_minCharacter] == null)
                {
                    m_next[currentCharacter - m_minCharacter] = new Trie();
                    ++m_liveNodes;
                    //alloc_Debug.Assert(next.table [c - min]);
                }
                return m_next[currentCharacter - m_minCharacter].Add(prefix, start + 1, size - 1);
            }
        }


        /// <summary>
        /// Remove key from the trie. Returns true if the item is actually
        /// removed from the trie.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="start"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool Remove([NotNull] byte[] prefix, int start, int size)
        {
            if (size == 0)
            {
                if (m_referenceCount == 0)
                    return false;
                m_referenceCount--;
                return m_referenceCount == 0;
            }

            byte currentCharacter = prefix[start];
            if (m_count == 0 || currentCharacter < m_minCharacter || currentCharacter >= m_minCharacter + m_count)
                return false;

            Trie nextNode = m_count == 1 ? m_next[0] : m_next[currentCharacter - m_minCharacter];

            if (nextNode == null)
                return false;

            bool wasRemoved = nextNode.Remove(prefix, start + 1, size - 1);

            if (nextNode.IsRedundant())
            {
                //delete next_node;
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
                        Trie node = null;
                        for (short i = 0; i < m_count; ++i)
                        {
                            if (m_next[i] != null)
                            {
                                node = m_next[i];
                                m_minCharacter = (byte)(i + m_minCharacter);
                                break;
                            }
                        }

                        Debug.Assert(node != null);

                        m_next = null;
                        m_next = new[] { node };
                        m_count = 1;
                    }
                    else if (currentCharacter == m_minCharacter)
                    {
                        // We can compact the table "from the left"
                        byte newMin = m_minCharacter;
                        for (short i = 1; i < m_count; ++i)
                        {
                            if (m_next[i] != null)
                            {
                                newMin = (byte)(i + m_minCharacter);
                                break;
                            }
                        }
                        Debug.Assert(newMin != m_minCharacter);

                        Debug.Assert(newMin > m_minCharacter);
                        Debug.Assert(m_count > newMin - m_minCharacter);
                        m_count = (short)(m_count - (newMin - m_minCharacter));

                        m_next = m_next.Resize(m_count, false);

                        m_minCharacter = newMin;
                    }
                    else if (currentCharacter == m_minCharacter + m_count - 1)
                    {
                        // We can compact the table "from the right"
                        short newCount = m_count;
                        for (short i = 1; i < m_count; ++i)
                        {
                            if (m_next[m_count - 1 - i] != null)
                            {
                                newCount = (short)(m_count - i);
                                break;
                            }
                        }
                        Debug.Assert(newCount != m_count);
                        m_count = newCount;

                        m_next = m_next.Resize(m_count, true);
                    }
                }
            }

            return wasRemoved;
        }

        /// <summary>
        /// Check whether particular key is in the trie.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool Check([NotNull] byte[] data, int offset, int size)
        {
            // This function is on critical path. It deliberately doesn't use
            // recursion to get a bit better performance.
            Trie current = this;
            int start = offset;
            while (true)
            {
                // We've found a corresponding subscription!
                if (current.m_referenceCount > 0)
                    return true;

                // We've checked all the data and haven't found matching subscription.
                if (size == 0)
                    return false;

                // If there's no corresponding slot for the first character
                // of the prefix, the message does not match.
                byte character = data[start];
                if (character < current.m_minCharacter || character >= current.m_minCharacter + current.m_count)
                    return false;

                // Move to the next character.
                if (current.m_count == 1)
                    current = current.m_next[0];
                else
                {
                    current = current.m_next[character - current.m_minCharacter];

                    if (current == null)
                        return false;
                }
                start++;
                size--;
            }
        }

        // Apply the function supplied to each subscription in the trie.
        public void Apply([NotNull] TrieDelegate func, [CanBeNull] object arg)
        {
            ApplyHelper(null, 0, 0, func, arg);
        }

        private void ApplyHelper([NotNull] byte[] buffer, int bufferSize, int maxBufferSize, [NotNull] TrieDelegate func, [CanBeNull] object arg)
        {
            // If this node is a subscription, apply the function.
            if (m_referenceCount > 0)
                func(buffer, bufferSize, arg);

            // Adjust the buffer.
            if (bufferSize >= maxBufferSize)
            {
                maxBufferSize = bufferSize + 256;
                Array.Resize(ref buffer, maxBufferSize);
                Debug.Assert(buffer != null);
            }

            // If there are no subnodes in the trie, return.
            if (m_count == 0)
                return;

            // If there's one subnode (optimisation).
            if (m_count == 1)
            {
                buffer[bufferSize] = m_minCharacter;
                bufferSize++;
                m_next[0].ApplyHelper(buffer, bufferSize, maxBufferSize, func, arg);
                return;
            }

            // If there are multiple subnodes.
            for (short c = 0; c != m_count; c++)
            {
                buffer[bufferSize] = (byte)(m_minCharacter + c);
                if (m_next[c] != null)
                    m_next[c].ApplyHelper(buffer, bufferSize + 1, maxBufferSize, func, arg);
            }
        }

        private bool IsRedundant()
        {
            return m_referenceCount == 0 && m_liveNodes == 0;
        }
    }
}
