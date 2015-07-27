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

using System.Diagnostics;
using JetBrains.Annotations;

namespace NetMQ.Core.Patterns
{
    internal sealed class Pair : SocketBase
    {
        public class PairSession : SessionBase
        {
            public PairSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        private Pipe m_pipe;

        public Pair([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_options.SocketType = ZmqSocketType.Pair;
        }

        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">not used</param>
        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);

            // ZMQ_PAIR socket can only be connected to a single peer.
            // The socket rejects any further connection requests.
            if (m_pipe == null)
                m_pipe = pipe;
            else
                pipe.Terminate(false);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {
            if (pipe == m_pipe)
                m_pipe = null;
        }

        /// <summary>
        /// Indicate the given pipe as being ready for reading by this socket
        /// - however in the case of Pair, this does nothing.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for reading</param>
        protected override void XReadActivated(Pipe pipe)
        {
            // There's just one pipe. No lists of active and inactive pipes.
            // There's nothing to do here.
        }


        /// <summary>
        /// Indicate the given pipe as being ready for writing to by this socket,
        /// however in the case of this Pair socket - this does nothing.
        /// This method gets called by the WriteActivated method.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for writing</param>
        protected override void XWriteActivated(Pipe pipe)
        {
            // There's just one pipe. No lists of active and inactive pipes.
            // There's nothing to do here.
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        protected override bool XSend(ref Msg msg)
        {
            if (m_pipe == null || !m_pipe.Write(ref msg))
                return false;

            if (!msg.HasMore)
                m_pipe.Flush();

            // Detach the original message from the data buffer.
            msg.InitEmpty();

            return true;
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected override bool XRecv(ref Msg msg)
        {
            // Deallocate old content of the message.
            msg.Close();

            if (m_pipe == null || !m_pipe.Read(ref msg))
            {
                msg.InitEmpty();
                return false;
            }

            return true;
        }

        protected override bool XHasIn()
        {
            return m_pipe != null && m_pipe.CheckRead();
        }

        protected override bool XHasOut()
        {
            return m_pipe != null && m_pipe.CheckWrite();
        }
    }
}