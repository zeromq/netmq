/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    internal sealed class Pull : SocketBase
    {
        public class PullSession : SessionBase
        {
            public PullSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        /// <summary>
        /// Fair queueing object for inbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;

        public Pull([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_options.SocketType = ZmqSocketType.Pull;

            m_fairQueueing = new FairQueueing();
        }

        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">not used</param>
        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);
            m_fairQueueing.Attach(pipe);
        }

        /// <summary>
        /// Indicate the given pipe as being ready for reading by this socket.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for reading</param>
        protected override void XReadActivated(Pipe pipe)
        {
            m_fairQueueing.Activated(pipe);
        }

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {
            m_fairQueueing.Terminated(pipe);
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected override bool XRecv(ref Msg msg)
        {
            return m_fairQueueing.Recv(ref msg);
        }

        protected override bool XHasIn()
        {
            return m_fairQueueing.HasIn();
        }
    }
}