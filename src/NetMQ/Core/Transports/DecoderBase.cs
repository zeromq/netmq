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
        private ByteArraySegment? m_readPos;

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
        }

        public Endianness Endian { get; }
        
        /// <summary>
        /// Returns a buffer to be filled with binary data.
        /// </summary>
        public virtual void GetBuffer(out ByteArraySegment data, out int size)
        {
            // If we are expected to read large message, we'll opt for zero-
            // copy, i.e. we'll ask caller to fill the data directly to the
            // message. Note that subsequent read(s) are non-blocking, thus
            // each single read reads at most SO_RCVBUF bytes at once not
            // depending on how large is the chunk returned from here.
            // As a consequence, large messages being received won't block
            // other engines running in the same I/O thread for excessive
            // amounts of time.

            Assumes.NotNull(m_readPos);

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
        /// GetBuffer function. size argument specifies the number of bytes
        /// actually filled into the buffer.
        /// </summary>
        public virtual DecodeResult Decode (ByteArraySegment data, int size, out int bytesUsed)
        {
            Assumes.NotNull(m_readPos);

            bytesUsed = 0;
            
            // In case of zero-copy simply adjust the pointers, no copying
            // is required. Also, run the state machine in case all the data
            // were processed.
            if (data != null && data.Equals(m_readPos))
            {
                m_readPos.AdvanceOffset(size);
                m_toRead -= size;
                bytesUsed = size;

                while (m_toRead == 0)
                {
                    var result = Next();
                    if (result != DecodeResult.Processing)
                        return result;
                }

                return DecodeResult.Processing;
            }

            while (bytesUsed < size) {
                // Copy the data from buffer to the message.
                int toCopy = Math.Min(m_toRead, size - bytesUsed);
                
                // Only copy when destination address is different from the
                // current address in the buffer.
                data!.CopyTo(bytesUsed, m_readPos, 0, toCopy);
                m_readPos.AdvanceOffset(toCopy);
                m_toRead -= toCopy;
                bytesUsed += toCopy;
                
                // Try to get more space in the message to fill in.
                // If none is available, return.
                while (m_toRead == 0)
                {
                    var result = Next();
                    if (result != DecodeResult.Processing)
                        return result;
                }
            }

            return DecodeResult.Processing;
        }

        protected void NextStep(ByteArraySegment readPos, int toRead, int state)
        {
            m_readPos = readPos;
            m_toRead = toRead;
            State = state;
        }

        protected int State
        {
            get;
            set;
        }

        public abstract PushMsgResult PushMsg(ProcessMsgDelegate sink);


        protected abstract DecodeResult Next();
    }
}
