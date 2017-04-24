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

using System;
using System.Diagnostics;

namespace NetMQ.Core.Transports
{
    /// <summary>
    /// Helper base class for decoders that know the amount of data to read
    /// in advance at any moment.
    /// This class is the state machine that parses the incoming buffer.
    /// Derived classes should implement individual state machine actions.
    /// </summary>
    /// <remarks>
    /// Knowing the amount in advance is a property
    /// of the protocol used. 0MQ framing protocol is based size-prefixed
    /// paradigm, which qualifies it to be parsed by this class.
    ///
    /// On the other hand, XML-based transports (like XMPP or SOAP) don't allow
    /// for knowing the size of data to read in advance and should use different
    /// decoding algorithms.
    /// </remarks>
    internal abstract class DecoderBase : IDecoder
    {
        /// <summary>
        /// Where to store the read data.
        /// </summary>
        private ByteArraySegment m_readPos;

        /// <summary>
        /// How much data to read before taking next step.
        /// </summary>
        protected int m_toRead;

        /// <summary>
        /// The buffer for data to decode.
        /// </summary>
        private readonly int m_bufsize;
        private readonly byte[] m_buf;

        public DecoderBase(int bufsize, Endianness endian)
        {
            Endian = endian;
            m_toRead = 0;
            m_bufsize = bufsize;

            // TODO: use buffer pool
            m_buf = new byte[bufsize];
            State = -1;
        }

        public Endianness Endian { get; }

        public abstract void SetMsgSink(IMsgSink msgSink);


        /// <summary>
        /// Returns true if the decoder has been fed all required data
        /// but cannot proceed with the next decoding step.
        /// False is returned if the decoder has encountered an error.
        /// </summary>
        public virtual bool Stalled()
        {
            // Check whether there was decoding error.
            if (!Next())
                return false;

            while (m_toRead == 0)
            {
                if (!Next())
                {
                    if (!Next())
                        return false;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a buffer to be filled with binary data.
        /// </summary>
        public void GetBuffer(out ByteArraySegment data, out int size)
        {
            // If we are expected to read large message, we'll opt for zero-
            // copy, i.e. we'll ask caller to fill the data directly to the
            // message. Note that subsequent read(s) are non-blocking, thus
            // each single read reads at most SO_RCVBUF bytes at once not
            // depending on how large is the chunk returned from here.
            // As a consequence, large messages being received won't block
            // other engines running in the same I/O thread for excessive
            // amounts of time.

            if (m_toRead >= m_bufsize)
            {
                data = m_readPos.Clone();
                size = m_toRead;
                return;
            }

            data = new ByteArraySegment(m_buf);
            size = m_bufsize;
        }


        /// <summary>
        /// Processes the data in the buffer previously allocated using
        /// get_buffer function. size argument specifies the number of bytes
        /// actually filled into the buffer. Function returns number of
        /// bytes actually processed.
        /// </summary>
        public int ProcessBuffer(ByteArraySegment data, int size)
        {
            // Check if we had an error in previous attempt.
            if (State < 0)
            {
                return -1;
            }

            // In case of zero-copy simply adjust the pointers, no copying
            // is required. Also, run the state machine in case all the data
            // were processed.
            if (data != null && data.Equals(m_readPos))
            {
                m_readPos.AdvanceOffset(size);
                m_toRead -= size;

                while (m_toRead == 0)
                {
                    if (!Next())
                    {
                        if (State < 0)
                        {
                            return -1;
                        }
                        return size;
                    }
                }
                return size;
            }

            int pos = 0;
            while (true)
            {

                // Try to get more space in the message to fill in.
                // If none is available, return.
                while (m_toRead == 0)
                {
                    if (!Next())
                    {
                        if (State < 0)
                        {
                            return -1;
                        }

                        return pos;
                    }
                }

                // If there are no more data in the buffer, return.
                if (pos == size)
                    return pos;

                // Copy the data from buffer to the message.
                int toCopy = Math.Min(m_toRead, size - pos);
                data.CopyTo(pos, m_readPos, 0, toCopy);
                m_readPos.AdvanceOffset(toCopy);
                pos += toCopy;
                m_toRead -= toCopy;
            }
        }

        protected void NextStep(ByteArraySegment readPos, int toRead, int state)
        {
            m_readPos = readPos;
            m_toRead = toRead;
            this.State = state;
        }

        protected int State
        {
            get;
            set;
        }

        protected void DecodingError()
        {
            State = -1;
        }

        public virtual bool MessageReadySize(int msgSize)
        {
            Debug.Assert(false);
            return false;
        }

        protected abstract bool Next();
    }
}
