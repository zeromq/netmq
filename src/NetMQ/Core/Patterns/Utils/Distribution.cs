/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
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

using System.Collections.Generic;
using JetBrains.Annotations;

namespace NetMQ.Core.Patterns.Utils
{
    internal sealed class Distribution
    {
        /// <summary>
        /// List of outbound pipes.
        /// </summary>
        private readonly List<Pipe> m_pipes;

        /// <summary>
        /// Number of all the pipes to send the next message to.
        /// </summary>
        private int m_matching;

        /// <summary>
        /// Number of active pipes. All the active pipes are located at the
        /// beginning of the pipes array. These are the pipes the messages
        /// can be sent to at the moment.
        /// </summary>
        private int m_active;

        /// <summary>
        /// Number of pipes eligible for sending messages to. This includes all
        /// the active pipes plus all the pipes that we can in theory send
        /// messages to (the HWM is not yet reached), but sending a message
        /// to them would result in partial message being delivered, ie. message
        /// with initial parts missing.
        /// </summary>
        private int m_eligible;

        /// <summary>
        /// True if last we are in the middle of a multipart message.
        /// </summary>
        private bool m_more;

        /// <summary>
        /// Create a new, empty Distribution object.
        /// </summary>
        public Distribution()
        {
            m_pipes = new List<Pipe>();
        }

        /// <summary>
        /// Adds the pipe to the distributor object.
        /// </summary>
        /// <param name="pipe"></param>
        public void Attach([NotNull] Pipe pipe)
        {
            // If we are in the middle of sending a message, we'll add new pipe
            // into the list of eligible pipes. Otherwise we add it to the list
            // of active pipes.
            if (m_more)
            {
                m_pipes.Add(pipe);
                m_pipes.Swap(m_eligible, m_pipes.Count - 1);
                m_eligible++;
            }
            else
            {
                m_pipes.Add(pipe);
                m_pipes.Swap(m_active, m_pipes.Count - 1);
                m_active++;
                m_eligible++;
            }
        }

        /// <summary>
        /// Mark the pipe as matching. Subsequent call to send_to_matching
        /// will send message also to this pipe.
        /// </summary>
        /// <param name="pipe"></param>
        public void Match([NotNull] Pipe pipe)
        {
            int index = m_pipes.IndexOf(pipe);

            // If pipe is already matching do nothing.
            if (index < m_matching)
                return;

            // If the pipe isn't eligible, ignore it.
            if (index >= m_eligible)
                return;

            // Mark the pipe as matching.
            m_pipes.Swap(index, m_matching);
            m_matching++;
        }

        /// <summary>
        /// Mark all pipes as non-matching.
        /// </summary>
        public void Unmatch()
        {
            m_matching = 0;
        }

        /// <summary>
        /// This gets called by ProcessPipeTermAck or XTerminated to respond to the termination of the given pipe from the distributor.
        /// </summary>
        /// <param name="pipe">the pipe that was terminated</param>
        public void Terminated([NotNull] Pipe pipe)
        {
            // Remove the pipe from the list; adjust number of matching, active and/or
            // eligible pipes accordingly.
            if (m_pipes.IndexOf(pipe) < m_matching)
                m_matching--;
            if (m_pipes.IndexOf(pipe) < m_active)
                m_active--;
            if (m_pipes.IndexOf(pipe) < m_eligible)
                m_eligible--;
            m_pipes.Remove(pipe);
        }

        /// <summary>
        /// Activates pipe that have previously reached high watermark.
        /// </summary>
        /// <param name="pipe"></param>
        public void Activated([NotNull] Pipe pipe)
        {
            // Move the pipe from passive to eligible state.
            m_pipes.Swap(m_pipes.IndexOf(pipe), m_eligible);
            m_eligible++;

            // If there's no message being sent at the moment, move it to
            // the active state.
            if (!m_more)
            {
                m_pipes.Swap(m_eligible - 1, m_active);
                m_active++;
            }
        }

        /// <summary>
        /// Send the message to all the outbound pipes.
        /// </summary>
        /// <param name="msg"></param>
        public void SendToAll(ref Msg msg)
        {
            m_matching = m_active;
            SendToMatching(ref msg);
        }

        /// <summary>
        /// Send the message to the matching outbound pipes.
        /// </summary>
        /// <param name="msg"></param>
        public void SendToMatching(ref Msg msg)
        {
            // Is this end of a multipart message?
            bool hasMore = msg.HasMore;

            // Push the message to matching pipes.
            Distribute(ref msg);

            // If multipart message is fully sent, activate all the eligible pipes.
            if (!hasMore)
                m_active = m_eligible;

            m_more = hasMore;
        }

        /// <summary>
        /// Put the message to all active pipes.
        /// </summary>
        private void Distribute(ref Msg msg)
        {
            // If there are no matching pipes available, simply drop the message.
            if (m_matching == 0)
            {
                msg.Close();
                msg.InitEmpty();

                return;
            }

            if (msg.MsgType != MsgType.Pool)
            {
                for (int i = 0; i < m_matching; ++i)
                {
                    if (!Write(m_pipes[i], ref msg))
                    {
                        --i; //  Retry last write because index will have been swapped
                    }
                }

                msg.Close();
                msg.InitEmpty();

                return;
            }

            // Add matching-1 references to the message. We already hold one reference,
            // that's why -1.
            msg.AddReferences(m_matching - 1);

            // Push copy of the message to each matching pipe.
            int failed = 0;
            for (int i = 0; i < m_matching; ++i)
            {
                if (!Write(m_pipes[i], ref msg))
                {
                    ++failed;
                    --i; //  Retry last write because index will have been swapped
                }
            }
            if (failed != 0)
                msg.RemoveReferences(failed);

            // Detach the original message from the data buffer. Note that we don't
            // close the message. That's because we've already used all the references.
            msg.InitEmpty();
        }

        public bool HasOut()
        {
            return true;
        }

        /// <summary>
        /// Write the message to the pipe. Make the pipe inactive if writing
        /// fails. In such a case false is returned.
        /// </summary>
        private bool Write([NotNull] Pipe pipe, ref Msg msg)
        {
            if (!pipe.Write(ref msg))
            {
                m_pipes.Swap(m_pipes.IndexOf(pipe), m_matching - 1);
                m_matching--;
                m_pipes.Swap(m_pipes.IndexOf(pipe), m_active - 1);
                m_active--;
                m_pipes.Swap(m_active, m_eligible - 1);
                m_eligible--;
                return false;
            }
            if (!msg.HasMore)
                pipe.Flush();
            return true;
        }
    }
}
