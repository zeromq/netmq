using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NetMQ.Core.Patterns.Utils;

namespace NetMQ.Core.Patterns
{
    internal class Radio : SocketBase
    {
        private readonly Dictionary<string, HashSet<Pipe>> m_subscriptions;
        private readonly Distribution m_distribution;

        internal Radio(Ctx parent, int threadId, int socketId) : base(parent, threadId, socketId, true)
        {
            m_options.SocketType = ZmqSocketType.Radio;

            m_subscriptions = new Dictionary<string, HashSet<Pipe>>();
            m_distribution = new Distribution();
        }

        protected override void XAttachPipe(Pipe pipe, bool icanhasall)
        {
            pipe.SetNoDelay();
            m_distribution.Attach(pipe);
            XReadActivated(pipe);
        }

        protected override void XReadActivated(Pipe pipe)
        {
            // There are some subscriptions waiting. Let's process them.
            Msg msg = new Msg();
            while (pipe.Read(ref msg))
            {
                // Apply the subscription to the trie
                if (msg.IsJoin || msg.IsLeave)
                {
                    if (msg.IsJoin)
                    {
                        if (!m_subscriptions.TryGetValue(msg.Group, out var pipes))
                        {
                            pipes = new HashSet<Pipe>();
                            m_subscriptions.Add(msg.Group, pipes);
                        }

                        pipes.Add(pipe);
                    }
                    else
                    {
                        if (m_subscriptions.TryGetValue(msg.Group, out var pipes))
                        {
                            pipes.Remove(pipe);
                            if (!pipes.Any())
                                m_subscriptions.Remove(msg.Group);
                        }
                    }
                }

                msg.Close();
            }
        }

        protected override void XWriteActivated(Pipe pipe)
        {
            m_distribution.Activated(pipe);
        }

        protected override void XTerminated(Pipe pipe)
        {
            foreach (var pipes in m_subscriptions.Values)
                pipes.Remove(pipe);

            foreach (var p in m_subscriptions.Where(p => !p.Value.Any()).ToArray())
                m_subscriptions.Remove(p.Key);

            m_distribution.Terminated(pipe);
        }

        protected override bool XSend(ref Msg msg)
        {
            //  Radio sockets do not allow multipart data (ZMQ_SNDMORE)
            if (msg.HasMore)
                throw new InvalidException();

            m_distribution.Unmatch();

            if (m_subscriptions.TryGetValue(msg.Group, out var range))
            {
                foreach (var pipe in range)
                    m_distribution.Match(pipe);
            }

            m_distribution.SendToMatching(ref msg);

            return true;
        }

        protected override bool XHasOut() => m_distribution.HasOut();

        protected override bool XRecv(ref Msg msg)
        {
            // Messages cannot be received from RADIO socket.
            throw new NotSupportedException("Messages cannot be received from RADIO socket");
        }

        protected override bool XHasIn() => false;

        internal class RadioSession : SessionBase
        {
            enum State
            {
                Group,
                Body
            }

            private State m_state = State.Group;
            private Msg m_pending = new Msg();

            public RadioSession(IOThread ioThread, bool connect, SocketBase socket,
                Options options, Address addr) : base(ioThread, connect, socket, options, addr)
            {
            }

            public override PushMsgResult PushMsg(ref Msg msg)
            {
                if (msg.HasCommand)
                {
                    byte commandNameSize = msg[0];

                    if (msg.Size < commandNameSize + 1)
                        return base.PushMsg(ref msg);

                    string commandName = msg.GetString(Encoding.ASCII, 1, commandNameSize);

                    int groupLength;
                    string group;
                    Msg joinLeaveMsg = new Msg();

                    // Set the msg type to either JOIN or LEAVE
                    if (commandName == "JOIN")
                    {
                        groupLength = msg.Size - 5;
                        group = msg.GetString(Encoding.ASCII, 5, groupLength);
                        joinLeaveMsg.InitJoin();
                    }
                    else if (commandName == "LEAVE")
                    {
                        groupLength = msg.Size - 6;
                        group = msg.GetString(Encoding.ASCII, 6, groupLength);
                        joinLeaveMsg.InitLeave();
                    }
                    // If it is not a JOIN or LEAVE just push the message
                    else
                        return base.PushMsg(ref msg);

                    //  Set the group
                    joinLeaveMsg.Group = group;

                    //  Close the current command
                    msg.Close();

                    //  Push the join or leave command
                    msg = joinLeaveMsg;
                    return base.PushMsg(ref msg);
                }

                return base.PushMsg(ref msg);
            }

            public override PullMsgResult PullMsg(ref Msg msg)
            {
                switch (m_state)
                {
                    case State.Group:
                        var result = base.PullMsg(ref m_pending);
                        if (result != PullMsgResult.Ok)
                            return result;

                        //  First frame is the group
                        msg.InitPool(Encoding.ASCII.GetByteCount(m_pending.Group));
                        msg.SetFlags(MsgFlags.More);
                        msg.Put(Encoding.ASCII, m_pending.Group, 0);

                        //  Next status is the body
                        m_state = State.Body;
                        break;
                    case State.Body:
                        msg = m_pending;
                        m_state = State.Group;
                        break;
                }

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