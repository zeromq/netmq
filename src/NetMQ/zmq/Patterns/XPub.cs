/*      
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file
        
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
using System.Text;
using NetMQ.zmq.Patterns.Utils;

namespace NetMQ.zmq.Patterns
{
    class XPub : SocketBase
    {

        public class XPubSession : SessionBase
        {

            public XPubSession(IOThread ioThread, bool connect,
                                                 SocketBase socket, Options options, Address addr) :
                base(ioThread, connect, socket, options, addr)
            {

            }

        }

        //  List of all subscriptions mapped to corresponding pipes.
        private readonly MultiTrie m_subscriptions;

        //  Distributor of messages holding the list of outbound pipes.
        private readonly Distribution m_distribution;

        // If true, send all subscription messages upstream, not just
        // unique ones
        bool m_verbose;

        private bool m_manual;

        private Pipe m_lastPipe;

        private Msg m_welcomeMessage;

        //  True if we are in the middle of sending a multi-part message.
        private bool m_more;

        //  List of pending (un)subscriptions, ie. those that were already
        //  applied to the trie, but not yet received by the user.
        private readonly Queue<byte[]> m_pending;

        private static readonly MultiTrie.MultiTrieDelegate s_markAsMatching;
        private static readonly MultiTrie.MultiTrieDelegate s_SendUnsubscription;

        static XPub()
        {
            s_markAsMatching = (pipe, data,size, arg) =>
            {
                XPub self = (XPub)arg;
                self.m_distribution.Match(pipe);
            };

            s_SendUnsubscription = (pipe, data,size, arg) =>
            {

                XPub self = (XPub)arg;

                if (self.m_options.SocketType != ZmqSocketType.Pub)
                {
                    //  Place the unsubscription to the queue of pending (un)sunscriptions
                    //  to be retrived by the user later on.

                    byte[] unsub = new byte[size+1];
                    unsub[0] = 0;
                    Buffer.BlockCopy(data, 0, unsub, 1, size);
                    
                    self.m_pending.Enqueue(unsub);
                }
            };
        }

        public XPub(Ctx parent, int threadId, int socketId)
            : base(parent, threadId, socketId)
        {

            m_options.SocketType = ZmqSocketType.Xpub;
            m_verbose = false;
            m_manual = false;
            m_more = false;

            m_welcomeMessage = new Msg();
            m_welcomeMessage.InitEmpty();

            m_subscriptions = new MultiTrie();
            m_distribution = new Distribution();
            m_pending = new Queue<byte[]>();
        }                

        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            Debug.Assert(pipe != null);
            m_distribution.Attach(pipe);

            //  If icanhasall_ is specified, the caller would like to subscribe
            //  to all data on this pipe, implicitly.
            if (icanhasall)
                m_subscriptions.Add(null,0,0, pipe);

            // if welcome message was set
            if (m_welcomeMessage.Size > 0)
            {
                Msg copy = new Msg();
                copy.InitEmpty();
                copy.Copy(ref m_welcomeMessage);

                pipe.Write(ref copy);
                pipe.Flush();
            }

            //  The pipe is active when attached. Let's read the subscriptions from
            //  it, if any.
            XReadActivated(pipe);
        }

        protected override void XReadActivated(Pipe pipe)
        {
            //  There are some subscriptions waiting. Let's process them.
            Msg sub = new Msg();
            while (pipe.Read(ref sub))
            {
                //  Apply the subscription to the trie.
                byte[] data = sub.Data;
                int size = sub.Size;
                if (size > 0 && (data[0] == 0 || data[0] == 1))
                {
                    if (m_manual)
                    {
                        m_lastPipe = pipe;                        

                        m_pending.Enqueue(GetBytesFromMsg(ref sub));
                    }
                    else
                    {
                        bool unique;
                        if (data[0] == 0)
                            unique = m_subscriptions.Remove(data, 1, size - 1, pipe);
                        else
                            unique = m_subscriptions.Add(data, 1, size - 1, pipe);

                        //  If the subscription is not a duplicate, store it so that it can be
                        //  passed to used on next recv call.
                        if (m_options.SocketType == ZmqSocketType.Xpub && (unique || m_verbose))
                            m_pending.Enqueue(GetBytesFromMsg(ref sub));
                    }
                }
                else // process message unrelated to sub/unsub
                {
                    m_pending.Enqueue(GetBytesFromMsg(ref sub));
                }
                
                sub.Close();
            }
        }

        protected override void XWriteActivated(Pipe pipe)
        {
            m_distribution.Activated(pipe);
        }

        protected override bool XSetSocketOption(ZmqSocketOptions option, Object optval)
        {
            if (option == ZmqSocketOptions.XpubVerbose)
            {
                m_verbose = (int)optval == 1;
                return true;
            }
            if (option == ZmqSocketOptions.XPublisherManual)
            {
                m_manual = true;
                return true;
            }            
            else if (option == ZmqSocketOptions.Subscribe && m_manual && m_lastPipe != null)
            {
                byte[] subscription;

                if (optval is byte[])
                {
                    subscription = optval as byte[];
                }
                else
                {
                   subscription = Encoding.ASCII.GetBytes((String)optval);
                }

                m_subscriptions.Add(subscription, 0, subscription.Length, m_lastPipe);
                return true;
            }
            else if (option == ZmqSocketOptions.Unsubscribe && m_manual && m_lastPipe != null)
            {
                byte[] subscription;

                if (optval is byte[])
                {
                    subscription = optval as byte[];
                }
                else
                {
                    subscription = Encoding.ASCII.GetBytes((String)optval);
                }

                m_subscriptions.Remove(subscription, 0, subscription.Length, m_lastPipe);
                return true;
            }
            else if (option == ZmqSocketOptions.XPublisherWelcomeMessage)
            {
                m_welcomeMessage.Close();

                if (optval != null)
                {                    
                    if (optval is byte[])
                    {
                        byte[] value = (byte[]) optval;

                        byte[] welcomeBytes = new byte[value.Length];
                        value.CopyTo(welcomeBytes, 0);

                        m_welcomeMessage.InitGC(welcomeBytes, welcomeBytes.Length);
                    }
                    else
                    {
                        throw new InvalidException();
                    }                    
                }
                else
                {
                    m_welcomeMessage.InitEmpty();
                }

                return true;
            }

            return false;
        }

        protected override void XTerminated(Pipe pipe)
        {
            //  Remove the pipe from the trie. If there are topics that nobody
            //  is interested in anymore, send corresponding unsubscriptions
            //  upstream.


            m_subscriptions.RemoveHelper(pipe, s_SendUnsubscription, this);

            m_distribution.Terminated(pipe);
        }

        protected override bool XSend(ref Msg msg, SendReceiveOptions flags)
        {
            bool msgMore = msg.HasMore;

            //  For the first part of multi-part message, find the matching pipes.
            if (!m_more)
                m_subscriptions.Match(msg.Data, msg.Size,
                                                        s_markAsMatching, this);

            //  Send the message to all the pipes that were marked as matching
            //  in the previous step.
            m_distribution.SendToMatching(ref msg, flags);

            //  If we are at the end of multi-part message we can mark all the pipes
            //  as non-matching.
            if (!msgMore)
                m_distribution.Unmatch();

            m_more = msgMore;

            return true;
        }


        protected override bool XHasOut()
        {
            return m_distribution.HasOut();
        }

        protected override bool XRecv(SendReceiveOptions flags, ref Msg msg)
        {
            //  If there is at least one 
            if (m_pending.Count == 0)
            {                
                return false;
            }

            msg.Close();
            
            byte[] first = m_pending.Dequeue();
            msg.InitPool(first.Length);

            msg.Put(first,0, first.Length);

            return true;
        }

        protected override bool XHasIn()
        {
            return m_pending.Count != 0;
        }

        private byte[] GetBytesFromMsg(ref Msg msg)
        {
            byte[] bytes = new byte[msg.Size];
            Buffer.BlockCopy(msg.Data, 0, bytes, 0, msg.Size);

            return bytes;
        }
    }
}
