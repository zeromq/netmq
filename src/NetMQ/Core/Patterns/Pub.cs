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

using System;
using JetBrains.Annotations;

namespace NetMQ.Core.Patterns
{
    internal sealed class Pub : XPub
    {
        public class PubSession : XPubSession
        {
            public PubSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            {}
        }

        public Pub([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_options.SocketType = ZmqSocketType.Pub;
        }

        /// <summary>
        /// This override of the abstract XRecv method, simply throws a NotSupportedException because XRecv is not supported on a Pub socket.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <exception cref="NotSupportedException">Messages cannot be received from PUB socket</exception>
        protected override bool XRecv(ref Msg msg)
        {
            // Messages cannot be received from PUB socket.
            throw new NotSupportedException("Messages cannot be received from PUB socket");
        }

        protected override bool XHasIn()
        {
            return false;
        }
    }
}