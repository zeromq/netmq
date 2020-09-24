using System;
using System.Collections.Generic;
using System.Text;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    internal class Dish : SocketBase
    {
        // Fair queueing object for inbound pipes.
        private readonly  FairQueueing m_fairQueueing = new FairQueueing();
        
        // Object for distributing the subscriptions upstream.
        private readonly Distribution m_distribution = new Distribution();
        
        // The repository of subscriptions.
        private readonly HashSet<string> m_subscriptions = new HashSet<string>();
        
        // If true, 'message' contains a matching message to return on the
        // next recv call.
        private bool m_hasMessage = false;
        private Msg m_message = new Msg();
        
        public Dish(Ctx parent, int threadId, int socketId) : base(parent, threadId, socketId,true)
        {
            m_options.SocketType = ZmqSocketType.Dish;
            
            // When socket is being closed down we don't want to wait till pending
            // subscription commands are sent to the wire.
            m_options.Linger = 0;
            
            m_message.InitEmpty();
        }

        public override void Destroy()
        {
            base.Destroy();
            m_message.Close();
        }

        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            m_fairQueueing.Attach(pipe);
            m_distribution.Attach(pipe);
            
            // Send all the cached subscriptions to the new upstream peer.
            SendSubscriptions(pipe);
        }

        protected override void XReadActivated(Pipe pipe)
        {
            m_fairQueueing.Activated(pipe);
        }

        protected override void XWriteActivated(Pipe pipe)
        {
            m_distribution.Activated(pipe);
        }

        protected override void XTerminated(Pipe pipe)
        {
            m_fairQueueing.Terminated(pipe);
            m_distribution.Terminated(pipe);
        }

        protected override void XHiccuped(Pipe pipe)
        {
            // Send all the cached subscriptions to the hiccuped pipe.
            SendSubscriptions(pipe);
        }

        protected override void XJoin(string @group)
        {
            if (group.Length > Msg.MaxGroupLength) 
                throw new InvalidException("Group maximum length is 255");

            // User cannot join same group twice
            if (!m_subscriptions.Add(@group))
                throw new InvalidException("Group was already joined");

            Msg msg = new Msg();
            msg.InitJoin();
            msg.Group = group;

            m_distribution.SendToAll(ref msg);
            msg.Close();      
        }

        protected override void XLeave(string @group)
        {
            if (group.Length > Msg.MaxGroupLength) 
                throw new InvalidException("Group maximum length is 255");

            if (!m_subscriptions.Remove(@group))
                throw new InvalidException("Socket didn't join group");

            Msg msg = new Msg();
            msg.InitLeave();
            msg.Group = group;

            m_distribution.SendToAll(ref msg);
            msg.Close();      
        }

        protected override bool XSend(ref Msg msg)
        {
            throw new NotSupportedException("XSend not supported on Dish socket");
        }
        
        protected override bool XHasOut() => true; //  Subscription can be added/removed anytime.

        protected override bool XRecv(ref Msg msg)
        {
            // If there's already a message prepared by a previous call to poll,
            // return it straight ahead.
            if (m_hasMessage) 
            {
                msg.Move(ref m_message);
                m_hasMessage = false;
                return true;
            }

            return XXRecv(ref msg);
        }

        bool XXRecv(ref Msg msg)
        {
            // Get a message using fair queueing algorithm.
            bool received = m_fairQueueing.Recv(ref msg);

            // If there's no message available, return immediately.
            // The same when error occurs.
            if (!received)
                return false;
            
            // Skip non matching messages
            while (!m_subscriptions.Contains(msg.Group))
            {
                received = m_fairQueueing.Recv(ref msg);
                if (!received)
                    return false;
            } 

            //  Found a matching message
            return true;
        }

        protected override bool XHasIn()
        {
            // If there's already a message prepared by a previous call to zmq_poll,
            // return straight ahead.
            if (m_hasMessage)
                return true;

            var received = XXRecv(ref m_message);
            if (!received)
                return false;

            //  Matching message found
            m_hasMessage = true;
            return true;
        }

        private void SendSubscriptions(Pipe pipe)
        {
            foreach (var subscription in m_subscriptions)
            {
                Msg msg = new Msg();
                msg.InitJoin();
                msg.Group = subscription;
                
                //  Send it to the pipe.
                pipe.Write(ref msg);
            }

            pipe.Flush();
        }

        internal class DishSession : SessionBase
        {
            enum State
            {
                Group,
                Body
            }

            private State m_state = State.Group;
            private string m_group = String.Empty;
            
            public DishSession(IOThread ioThread, bool connect, SocketBase socket, Options options, Address addr) : base(ioThread, connect, socket, options, addr)
            {
            }

            public override PushMsgResult PushMsg(ref Msg msg)
            {
                switch (m_state)
                {
                    case State.Group:
                        if (!msg.HasMore)
                            return PushMsgResult.Error;

                        if (msg.Size > Msg.MaxGroupLength)
                            return PushMsgResult.Error;

                        m_group = msg.GetString(Encoding.ASCII);
                        m_state = State.Body;
                        
                        msg.Close();
                        msg.InitEmpty();
                        
                        return PushMsgResult.Ok;
                    case State.Body:
                        //  Set the message group
                        msg.Group = m_group;
                        
                        //  Thread safe socket doesn't support multipart messages
                        if (msg.HasMore)
                            return PushMsgResult.Error;

                        //  Push message to dish socket
                        var result = base.PushMsg(ref msg);
                        if (result == PushMsgResult.Ok)
                            m_state = State.Group;
                        
                        return result;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public override PullMsgResult PullMsg(ref Msg msg)
            {
                var result = base.PullMsg(ref msg);
                if (result != PullMsgResult.Ok)
                    return result;

                if (!msg.IsJoin && !msg.IsLeave)
                    return PullMsgResult.Ok;

                Msg command = new Msg();
                int offset;

                if (msg.IsJoin)
                {
                    command.InitPool(msg.Group.Length + 5);
                    offset = 5;
                    command[0] = 4;
                    command.Put(Encoding.ASCII, "JOIN", 1);
                } 
                else 
                {
                    command.InitPool(msg.Group.Length + 6);
                    offset = 6;
                    command[0] = 5;
                    command.Put(Encoding.ASCII, "LEAVE", 1);
                }

                command.SetFlags(MsgFlags.Command);
                
                //  Copy the group
                command.Put(Encoding.ASCII, msg.Group, offset);

                //  Close the join message
                msg.Close();
                
                msg = command;

                return PullMsgResult.Ok;
            }

            protected override void Reset()
            {
                base.Reset();
                m_state = State.Group;
            }
        }
    }
}
