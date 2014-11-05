using System;
using System.Diagnostics;

namespace NetMQ.Core
{
    class IOThreadMailbox : IMailbox
    {
        private readonly Proactor m_procator;

        private readonly IMailboxEvent m_mailboxEvent;

        private readonly YPipe<Command> m_cpipe;

        //  There's only one thread receiving from the mailbox, but there
        //  is arbitrary number of threads sending. Given that ypipe requires
        //  synchronised access on both of its endpoints, we have to synchronise
        //  the sending side.
        private readonly object m_sync;

        // mailbox name, for better debugging
        private readonly String m_name;

        private bool m_disposed ;

        public IOThreadMailbox(string name, Proactor proactor, IMailboxEvent mailboxEvent)
        {
            m_procator = proactor;
            m_mailboxEvent = mailboxEvent;

            m_cpipe = new YPipe<Command>(Config.CommandPipeGranularity, "mailbox");
            m_sync = new object();

            //  Get the pipe into passive state. That way, if the users starts by
            //  polling on the associated file descriptor it will get woken up when
            //  new command is posted.
            Command cmd = new Command();

            bool ok = m_cpipe.Read(ref cmd);
            Debug.Assert(!ok);

            m_name = name;

            m_disposed = false;
        }

        public void Send(Command command)
        {
            bool ok = false;
            lock (m_sync)
            {
                m_cpipe.Write(ref command, false);
                ok = m_cpipe.Flush();
            }

            if (!ok)
            {
                m_procator.SignalMailbox(this);
            }
        }

        public Command Recv()
        {            
            Command cmd = null;
            bool ok;

            ok = m_cpipe.Read(ref cmd);

            return cmd;
        }

        public void RaiseEvent()
        {
            if (!m_disposed)
            {
                m_mailboxEvent.Ready();
            }
        }

        public void Close()
        {
            m_disposed = true;
        }
    }
}