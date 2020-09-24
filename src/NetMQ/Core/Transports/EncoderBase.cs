/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
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

namespace NetMQ.Core.Transports
{
    internal abstract class EncoderBase : IEncoder
    {
        /// <summary>
        /// Where to get the data to write from.
        /// </summary>
        private ByteArraySegment? m_writePos;
        
        private bool m_newMsgFlag;

        /// <summary>
        /// How much data to write before the next step should be executed.
        /// </summary>
        private int m_toWrite;

        /// <summary>
        /// The buffer for encoded data.
        /// </summary>
        private readonly byte[] m_buffer;

        /// <summary>
        /// The size of the encoded-data buffer
        /// </summary>
        private readonly int m_bufferSize;

        protected Msg m_inProgress;
        private bool m_hasMessage;

        /// <summary>
        /// Create a new EncoderBase with a buffer of the given size.
        /// </summary>
        /// <param name="bufferSize">how big of an internal buffer to allocate (in bytes)</param>
        /// <param name="endian">the <see cref="Endianness"/> to set this EncoderBase to</param>
        protected EncoderBase(int bufferSize, Endianness endian)
        {
            Endian = endian;
            m_bufferSize = bufferSize;
            m_buffer = new byte[bufferSize];
            m_inProgress.InitEmpty();
            m_hasMessage = false;
            m_newMsgFlag = false;
        }

        public void Dispose()
        {
            if (m_inProgress.IsInitialised)
            m_inProgress.Close();
        }

        /// <summary>
        /// Get the Endianness (Big or Little) that this EncoderBase uses.
        /// </summary>
        public Endianness Endian { get; }
        
        public int Encode(ref ByteArraySegment? data, int size)
        {
            ByteArraySegment buffer = data ?? new ByteArraySegment(m_buffer);
            int bufferSize = data == null ? m_bufferSize : size;

            if (!m_hasMessage)
                return 0;

            int pos = 0;
            while (pos < bufferSize)
            {
                // If there are no more data to return, run the state machine.
                // If there are still no data, return what we already have
                // in the buffer.
                if (m_toWrite == 0)
                {
                    if (m_newMsgFlag) {
                        m_inProgress.Close();
                        m_inProgress.InitEmpty();
                        m_hasMessage = false;
                        break;
                    }

                    Next();
                }

                // If there are no data in the buffer yet and we are able to
                // fill whole buffer in a single go, let's use zero-copy.
                // There's no disadvantage to it as we cannot stuck multiple
                // messages into the buffer anyway. Note that subsequent
                // write(s) are non-blocking, thus each single write writes
                // at most SO_SNDBUF bytes at once not depending on how large
                // is the chunk returned from here.
                // As a consequence, large messages being sent won't block
                // other engines running in the same I/O thread for excessive
                // amounts of time.
                if (pos == 0 && data == null && m_toWrite >= bufferSize)
                {
                    data = m_writePos;
                    pos = m_toWrite;

                    m_writePos = null;
                    m_toWrite = 0;
                    return pos;
                }

                // Copy data to the buffer. If the buffer is full, return.
                int toCopy = Math.Min(m_toWrite, bufferSize - pos);

                if (toCopy != 0)
                {
                    Assumes.NotNull(m_writePos);

                    m_writePos.CopyTo(0, buffer, pos, toCopy);
                    pos += toCopy;
                    m_writePos.AdvanceOffset(toCopy);
                    m_toWrite -= toCopy;
                }
            }

            data = buffer;
            return pos;
        }
        
        public void LoadMsg (ref Msg msg)
        {
            m_inProgress = msg;
            m_hasMessage = true;
            msg.InitEmpty();
            Next();
        }

        protected int State { get; private set; }

        protected abstract void Next();

        protected void NextStep(ByteArraySegment? writePos, int toWrite, int state, bool newMsgFlag)
        {
            m_writePos = writePos;
            m_toWrite = toWrite;
            State = state;
            m_newMsgFlag = newMsgFlag;
        }
    }
}
