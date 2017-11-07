/*
    Copyright (c) 2012 iMatix Corporation
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Core.Patterns.Utils;
using NetMQ.Core.Utils;

namespace NetMQ.Core.Patterns
{
    /// <summary>
    /// A Router is a subclass of SocketBase
    /// </summary>
    internal class Peer : SocketBase
    {
        private static readonly Random s_random = new Random();

        public class PeerSession : SessionBase
        {
            public PeerSession([NotNull] IOThread ioThread, bool connect, [NotNull] SocketBase socket, [NotNull] Options options, [NotNull] Address addr)
                : base(ioThread, connect, socket, options, addr)
            { }
        }

        /// <summary>
        /// An instance of class Outpipe contains a Pipe and a boolean property Active.
        /// </summary>
        private class Outpipe
        {
            public Outpipe([NotNull] Pipe pipe, bool active)
            {
                Pipe = pipe;
                Active = active;
            }

            [NotNull]
            public Pipe Pipe { get; }

            public bool Active;
        };
        
        enum State
        {
            RoutingId,
            Data
        }

        /// <summary>
        /// Fair queueing object for inbound pipes.
        /// </summary>
        private readonly FairQueueing m_fairQueueing;
    
        /// <summary>
        /// Holds the prefetched message.
        /// </summary>
        private Msg m_prefetchedMsg;
        
        /// <summary>
        /// Outbound pipes indexed by the peer IDs.
        /// </summary>
        private readonly Dictionary<byte[], Outpipe> m_outpipes;

        /// <summary>
        /// The pipe we are currently writing to.
        /// </summary>
        private Pipe m_currentOut;  
             
        /// <summary>
        /// State of the recv operation
        /// </summary>
        private State m_receivingState;
        
        /// <summary>
        /// State of the sending operation
        /// </summary>
        private State m_sendingState;

        /// <summary>
        /// Peer ID are generated. It's a simple increment and wrap-over
        /// algorithm. This value is the next ID to use (if not used already).
        /// </summary>
        private int m_nextPeerId;
        
        /// <summary>
        /// Create a new Router instance with the given parent-Ctx, thread-id, and socket-id.
        /// </summary>
        /// <param name="parent">the Ctx that will contain this Router</param>
        /// <param name="threadId">the integer thread-id value</param>
        /// <param name="socketId">the integer socket-id value</param>
        public Peer([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {
            m_nextPeerId = s_random.Next();
            m_options.SocketType = ZmqSocketType.Peer;
            m_fairQueueing = new FairQueueing();      
            m_prefetchedMsg = new Msg();
            m_prefetchedMsg.InitEmpty();            
            m_outpipes = new Dictionary<byte[], Outpipe>(new ByteArrayEqualityComparer());
            m_sendingState = State.RoutingId;
            m_receivingState = State.RoutingId;            
        }
   
        public override void Destroy()
        {
            base.Destroy();            
            m_prefetchedMsg.Close();
        }

        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">not used</param>
        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);

            byte[] routingId = BitConverter.GetBytes(m_nextPeerId);
                        
            pipe.RoutingId = routingId;
            m_outpipes.Add(routingId, new Outpipe(pipe, true));
            m_fairQueueing.Attach(pipe);
            
            // As this exposed to the user we make a copy to avoid an issue
            m_options.LastPeerRoutingId = BitConverter.GetBytes(m_nextPeerId);
            
            m_nextPeerId++;
        }      

        /// <summary>
        /// This is an override of the abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected override void XTerminated(Pipe pipe)
        {           
            m_outpipes.TryGetValue(pipe.RoutingId, out Outpipe old);
            m_outpipes.Remove(pipe.RoutingId);

            Debug.Assert(old != null);

            m_fairQueueing.Terminated(pipe);
            if (pipe == m_currentOut)
                m_currentOut = null;
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
        /// Indicate the given pipe as being ready for writing to by this socket.
        /// This gets called by the WriteActivated method.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for writing</param>
        protected override void XWriteActivated(Pipe pipe)
        {
            Outpipe outpipe = null;

            foreach (var it in m_outpipes)
            {
                if (it.Value.Pipe == pipe)
                {
                    Debug.Assert(!it.Value.Active);
                    it.Value.Active = true;
                    outpipe = it.Value;
                    break;
                }
            }

            Debug.Assert(outpipe != null);
        }

        /// <summary>
        /// Transmit the given message. The <c>Send</c> method calls this to do the actual sending.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        /// <exception cref="HostUnreachableException">The receiving host must be identifiable.</exception>
        protected override bool XSend(ref Msg msg)
        {
            // If this is the first part of the message it's the ID of the
            // peer to send the message to.
            if (m_sendingState == State.RoutingId)
            {
                Debug.Assert(m_currentOut == null);

                // If we have malformed message (prefix with no subsequent message)
                // then just silently ignore it.                
                if (msg.HasMore)
                {                                       
                    // Find the pipe associated with the routingId stored in the prefix.                    
                    var routingId = msg.Size == msg.Data.Length
                        ? msg.Data
                        : msg.CloneData();

                    if (m_outpipes.TryGetValue(routingId, out Outpipe op))
                    {
                        m_currentOut = op.Pipe;
                        if (!m_currentOut.CheckWrite())
                        {                            
                            op.Active = false;
                            m_currentOut = null;

                            if (!op.Pipe.Active)
                                throw new HostUnreachableException("In Peer.XSend");

                            return false;
                        }
                    }
                    else
                        throw new HostUnreachableException("In Peer.XSend");
                    
                    m_sendingState = State.Data;
                }

                // Detach the message from the data buffer.
                msg.Close();
                msg.InitEmpty();

                return true;
            }

            m_sendingState = State.RoutingId;
    
            //  Peer sockets do not allow multipart data (ZMQ_SNDMORE)
            if (msg.HasMore) 
                throw new InvalidException();
                                    
            // Push the message into the pipe. If there's no out pipe, just drop it.
            if (m_currentOut != null)
            {                
                bool ok = m_currentOut.Write(ref msg);
                
                if (ok)                
                    m_currentOut.Flush();

                m_currentOut = null;
            }
            else
            {
                msg.Close();
            }

            // Detach the message from the data buffer.
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
            if (m_receivingState == State.Data)
            {                
                msg.Move(ref m_prefetchedMsg);
                m_receivingState = State.RoutingId;
                                              
                return true;
            }

            var pipe = new Pipe[1];

            bool isMessageAvailable = m_fairQueueing.RecvPipe(pipe, ref msg);

            // Drop any messages with more flag
            while (isMessageAvailable && msg.HasMore)
            {
                // drop all frames of the current multi-frame message
                isMessageAvailable = m_fairQueueing.RecvPipe(pipe, ref msg);

                while (isMessageAvailable && msg.HasMore)
                    isMessageAvailable = m_fairQueueing.RecvPipe(pipe, ref msg);
                
                // get the new message
                isMessageAvailable = m_fairQueueing.RecvPipe(pipe, ref msg);
            }

            if (!isMessageAvailable)            
                return false;            

            Debug.Assert(pipe[0] != null);
           
            // We are at the beginning of a message.
            // Keep the message part we have in the prefetch buffer
            // and return the ID of the peer instead.
            m_prefetchedMsg.Move(ref msg);
            
            byte[] routingId = pipe[0].RoutingId;
            msg.InitPool(routingId.Length);
            msg.Put(routingId, 0, routingId.Length);
            msg.SetFlags(MsgFlags.More);
            m_receivingState = State.Data;
                       
            return true;
        }     

        protected override bool XHasIn()
        {
            return m_fairQueueing.HasIn();           
        }

        protected override bool XHasOut()
        {
            // In theory, PEER socket is always ready for writing. Whether actual
            // attempt to write succeeds depends on which pipe the message is going
            // to be routed to.
            return true;
        }   
    }
}