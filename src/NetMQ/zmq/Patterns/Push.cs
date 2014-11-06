/*      
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2010 iMatix Corporation
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file
                
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
using NetMQ.zmq.Patterns.Utils;

namespace NetMQ.zmq.Patterns
{
    class Push : SocketBase
    {
        public class PushSession : SessionBase
        {
            public PushSession(IOThread ioThread, bool connect,
                               SocketBase socket, Options options,
                               Address addr)
                : base(ioThread, connect, socket, options, addr)
            {
            }
        }

        //  Load balancer managing the outbound pipes.
        private readonly LoadBalancer m_loadBalancer;

        public Push(Ctx parent, int threadId, int sid) : base(parent, threadId, sid)
        {
            m_options.SocketType = ZmqSocketType.Push;

            m_loadBalancer = new LoadBalancer();
        }

        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);
            m_loadBalancer.Attach(pipe);
        }

        protected override void XWriteActivated(Pipe pipe)
        {
            m_loadBalancer.Activated(pipe);
        }

        protected override void XTerminated(Pipe pipe)
        {
            m_loadBalancer.Terminated(pipe);
        }

        protected override bool XSend(ref Msg msg, SendReceiveOptions flags)
        {
            return m_loadBalancer.Send(ref msg, flags);
        }

        protected override bool XHasOut()
        {
            return m_loadBalancer.HasOut();
        }
    }
}
