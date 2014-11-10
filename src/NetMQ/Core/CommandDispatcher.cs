using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Core
{
    class CommandDispatcher
    {
        public const int ContextThreadId = 0;
        
        private readonly Reaper m_reaper;
        private IMailbox[] m_slots;

        //  List of unused thread slots.
        private readonly Stack<int> m_emptySlots;

        public CommandDispatcher(IMailbox contextMailbox, Reaper reaper, IList<IOThread> threads, int maxSockets)
        {
            m_reaper = reaper;
            m_slots = new IMailbox[maxSockets + threads.Count + 2];
            m_emptySlots = new Stack<int>();

            m_slots[ContextThreadId] = contextMailbox;
            m_slots[Reaper.ReaperThreadId] = reaper.Mailbox;

            foreach (var thread in threads)
            {
                m_slots[thread.ThreadId] = thread.Mailbox;
            }

            //  In the unused part of the slot array, create a list of empty slots.
            for (int i = (int)m_slots.Length - 1;
                     i >= (int)threads.Count + 2; i--)
            {
                m_emptySlots.Push(i);
                m_slots[i] = null;
            }            
        }

        public int EmptySlots
        {
            get { return m_emptySlots.Count; }
        }

        //  Send command to the destination thread.
        private void SendCommand(Command command)
        {
            m_slots[command.Destination.ThreadId].Send(command);
        }

        internal void SendStop(ZObject destination)
        {
            //  'stop' command goes always from administrative thread to
            //  the current object. 
            Command cmd = new Command(destination, CommandType.Stop);
            SendCommand(cmd);
        }

        internal void SendPlug(Own destination, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            Command cmd = new Command(destination, CommandType.Plug);
            SendCommand(cmd);
        }

        internal void SendOwn(Own destination, Own obj)
        {
            destination.IncSeqnum();
            Command cmd = new Command(destination, CommandType.Own, obj);
            SendCommand(cmd);
        }

        internal void SendAttach(SessionBase destination, NetMQ.Transports.IEngine engine, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            Command cmd = new Command(destination, CommandType.Attach, engine);
            SendCommand(cmd);
        }

        internal void SendBind(Own destination, Pipe pipe, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            Command cmd = new Command(destination, CommandType.Bind, pipe);
            SendCommand(cmd);
        }

        internal void SendActivateRead(Pipe destination)
        {
            Command cmd = new Command(destination, CommandType.ActivateRead);
            SendCommand(cmd);
        }

        internal void SendActivateWrite(Pipe destination,
                                            long msgsRead)
        {
            Command cmd = new Command(destination, CommandType.ActivateWrite, msgsRead);
            SendCommand(cmd);
        }

        internal void SendHiccup(Pipe destination, Object pipe)
        {
            Command cmd = new Command(destination, CommandType.Hiccup, pipe);
            SendCommand(cmd);
        }

        internal void SendPipeTerm(Pipe destination)
        {
            Command cmd = new Command(destination, CommandType.PipeTerm);
            SendCommand(cmd);
        }

        internal void SendPipeTermAck(Pipe destination)
        {
            Command cmd = new Command(destination, CommandType.PipeTermAck);
            SendCommand(cmd);
        }

        internal void SendTermReq(Own destination, Own own)
        {
            Command cmd = new Command(destination, CommandType.TermReq, own);
            SendCommand(cmd);
        }

        internal void SendTerm(Own destination, int linger)
        {
            Command cmd = new Command(destination, CommandType.Term, linger);
            SendCommand(cmd);
        }

        internal void SendTermAck(Own destination)
        {
            Command cmd = new Command(destination, CommandType.TermAck);
            SendCommand(cmd);
        }

        internal void SendReap(SocketBase socket)
        {
            Command cmd = new Command(m_reaper, CommandType.Reap, socket);
            SendCommand(cmd);
        }

        internal void SendReaped()
        {
            Command cmd = new Command(m_reaper, CommandType.Reaped);
            SendCommand(cmd);
        }

        internal void SendDone()
        {
            Command cmd = new Command(null, CommandType.Done);
            m_slots[ContextThreadId].Send(cmd);
        }

        public int GetEmptySlot()
        {
            return m_emptySlots.Pop();
        }

        public void ReleaseSlot(int slot)
        {
            m_emptySlots.Push(slot);
            m_slots[slot] = null;
        }

        public void SetMailbox(int slot, IMailbox mailbox)
        {
            m_slots[slot] = mailbox;
        }
    }
}
