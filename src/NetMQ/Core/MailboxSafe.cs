using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NetMQ.Core.Utils;

namespace NetMQ.Core
{
    internal class MailboxSafe : IMailbox
    {
        /// <summary>
        /// The pipe to store actual commands.
        /// </summary>
        private readonly YPipe<Command> m_commandPipe = new YPipe<Command>(Config.CommandPipeGranularity, "mailbox");

        //  Synchronize access to the mailbox from receivers and senders
        private object m_sync;

        private List<Signaler> m_signalers = new List<Signaler>();

#if DEBUG
        /// <summary>Mailbox name. Only used for debugging.</summary>
        private readonly string m_name;
#endif

        /// <summary>
        /// Create a new MailboxSafe with the given name.
        /// </summary>
        /// <param name="name">the name to give this new Mailbox</param>
        /// <param name="sync">Synchronize access to the mailbox from receivers and senders</param>
        public MailboxSafe(string name, object sync)
        {
            m_sync = sync;

            // Get the pipe into passive state. That way, if the users starts by
            // polling on the associated file descriptor it will get woken up when
            // new command is posted.
            bool ok = m_commandPipe.TryRead(out Command cmd);
            Debug.Assert(!ok);

#if DEBUG
            m_name = name;
#endif
        }

        public void AddSignaler(Signaler signaler)
        {
            m_signalers.Add(signaler);
        }

        public void RemoveSignaler(Signaler signaler)
        {
            m_signalers.Remove(signaler);
        }

        public void ClearSignalers()
        {
            m_signalers.Clear();
        }

        public void Send(Command cmd)
        {
            lock (m_sync)
            {
                m_commandPipe.Write(ref cmd, false);
                bool ok = m_commandPipe.Flush();

                if (!ok)
                {
                    Monitor.PulseAll(m_sync);

                    foreach (var signaler in m_signalers)
                    {
                        signaler.Send();
                    }
                }
            }
        }

        public bool TryRecv(int timeout, out Command command)
        {
            //  Try to get the command straight away.
            if (m_commandPipe.TryRead(out command))
                return true;

            //  If the timeout is zero, it will be quicker to release the lock, giving other a chance to send a command
            //  and immediately relock it.
            if (timeout == 0)
            {
                Monitor.Exit(m_sync);
                Monitor.Enter(m_sync);
            }
            else
            {
                //  Wait for signal from the command sender.
                Monitor.Wait(m_sync, timeout);
            }

            //  Another thread may already fetch the command
            return m_commandPipe.TryRead(out command);
        }

        public void Close()
        {
            Monitor.Enter(m_sync);
            Monitor.Exit(m_sync);
        }

#if DEBUG
        /// <summary>
        /// Override ToString to provide the type-name, plus the Mailbox name within brackets.
        /// </summary>
        /// <returns>a string of the form Mailbox[name]</returns>
        public override string ToString()
        {
            return base.ToString() + "[" + m_name + "]";
        }
#endif
    }
}