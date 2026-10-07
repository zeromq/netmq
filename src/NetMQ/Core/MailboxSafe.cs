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

        //  The owning socket's lock: held by receivers, never taken by senders
        private object m_sync;

        //  Serialises senders and wakes waiting receivers. Senders take only this lock, never the
        //  owning socket's m_sync: a sender may already hold its own socket's lock (a pipe flush
        //  inside Send), and taking a second socket's lock there deadlocks two sockets sending
        //  to each other. Nothing else is acquired while this lock is held.
        private readonly object m_commandSync = new object();

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
            lock (m_commandSync)
                m_signalers.Add(signaler);
        }

        public void RemoveSignaler(Signaler signaler)
        {
            lock (m_commandSync)
                m_signalers.Remove(signaler);
        }

        public void ClearSignalers()
        {
            lock (m_commandSync)
                m_signalers.Clear();
        }

        public void Send(Command cmd)
        {
            lock (m_commandSync)
            {
                m_commandPipe.Write(ref cmd, false);
                bool ok = m_commandPipe.Flush();

                if (!ok)
                {
                    Monitor.PulseAll(m_commandSync);

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
                //  Wait for signal from the command sender, releasing the socket while waiting.
                int depth = 0;
                try
                {
                    lock (m_commandSync)
                    {
                        //  A command flushed before we took m_commandSync pulsed nobody; read it now.
                        if (m_commandPipe.TryRead(out command))
                            return true;

                        while (Monitor.IsEntered(m_sync))
                        {
                            Monitor.Exit(m_sync);
                            depth++;
                        }

                        Monitor.Wait(m_commandSync, timeout);
                    }
                }
                finally
                {
                    //  Retake the socket lock only after releasing m_commandSync, so m_sync is never
                    //  acquired while m_commandSync is held. Restore every level even if interrupted,
                    //  as Monitor.Wait(m_sync) did; the caller's Unlock depends on it.
                    bool interrupted = false;
                    while (depth > 0)
                    {
                        try
                        {
                            Monitor.Enter(m_sync);
                            depth--;
                        }
                        catch (ThreadInterruptedException)
                        {
                            interrupted = true;
                        }
                    }

                    //  Re-arm the interruption for the next blocking call rather than throwing here,
                    //  which would replace an exception already unwinding.
                    if (interrupted)
                        Thread.CurrentThread.Interrupt();
                }
            }

            //  Another thread may already fetch the command
            return m_commandPipe.TryRead(out command);
        }

        public void Close()
        {
            lock (m_sync)
            {
                lock (m_commandSync)
                {
                }
            }
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