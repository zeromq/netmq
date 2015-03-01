/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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

using System;
using System.Diagnostics;
using System.Net.Sockets;
using JetBrains.Annotations;
using NetMQ.zmq.Utils;

namespace NetMQ.zmq
{
    internal interface IMailbox
    {
        void Send([NotNull] Command command);

        void Close();
    }

    internal interface IMailboxEvent
    {
        void Ready();
    }

    internal class IOThreadMailbox : IMailbox
    {
        [NotNull] private readonly Proactor m_proactor;

        [NotNull] private readonly IMailboxEvent m_mailboxEvent;

        [NotNull] private readonly YPipe<Command> m_cpipe;

        /// <summary>
        /// There's only one thread receiving from the mailbox, but there
        /// is arbitrary number of threads sending. Given that ypipe requires
        /// synchronised access on both of its endpoints, we have to synchronize
        /// the sending side.
        /// </summary>
        [NotNull] private readonly object m_sync;

        /// <summary>
        /// mailbox name, for better debugging
        /// </summary>
        [CanBeNull] private readonly String m_name;

        private bool m_disposed;

        public IOThreadMailbox([CanBeNull] string name, [NotNull] Proactor proactor, [NotNull] IMailboxEvent mailboxEvent)
        {
            m_proactor = proactor;
            m_mailboxEvent = mailboxEvent;

            m_cpipe = new YPipe<Command>(Config.CommandPipeGranularity, "mailbox");
            m_sync = new object();

            //  Get the pipe into passive state. That way, if the users starts by
            //  polling on the associated file descriptor it will get woken up when
            //  new command is posted.
            var cmd = new Command();

            bool ok = m_cpipe.Read(ref cmd);
            Debug.Assert(!ok);

            m_name = name;

            m_disposed = false;
        }

        public void Send(Command command)
        {
            bool ok;
            lock (m_sync)
            {
                m_cpipe.Write(ref command, false);
                ok = m_cpipe.Flush();
            }

            if (!ok)
            {
                m_proactor.SignalMailbox(this);
            }
        }

        [CanBeNull]
        public Command Recv()
        {            
            Command cmd = null;

            // bool ok =
               m_cpipe.Read(ref cmd);

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

        public override String ToString()
        {
            return base.ToString() + "[" + m_name + "]";
        }
    }

    internal class Mailbox : IMailbox
    {
        /// <summary>
        /// The pipe to store actual commands.
        /// </summary>
        private readonly YPipe<Command> m_cpipe;

        /// <summary>
        /// Signaler to pass signals from writer thread to reader thread.
        /// </summary>
        private readonly Signaler m_signaler;

        /// <summary>
        /// There's only one thread receiving from the mailbox, but there
        /// is arbitrary number of threads sending. Given that ypipe requires
        /// synchronised access on both of its endpoints, we have to synchronize
        /// the sending side.
        /// </summary>
        private readonly object m_sync;

        /// <summary>
        /// True if the underlying pipe is active, ie. when we are allowed to
        /// read commands from it.
        /// </summary>
        private bool m_active;

        /// <summary>
        /// mailbox name, for better debugging
        /// </summary>
        private readonly String m_name;

        public Mailbox(String name)
        {
            m_cpipe = new YPipe<Command>(Config.CommandPipeGranularity, "mailbox");
            m_sync = new object();
            m_signaler = new Signaler();

            //  Get the pipe into passive state. That way, if the users starts by
            //  polling on the associated file descriptor it will get woken up when
            //  new command is posted.

            var cmd = new Command();

            bool ok = m_cpipe.Read(ref cmd);
            Debug.Assert(!ok);
            m_active = false;

            m_name = name;
        }

        [NotNull]
        public Socket Handle
        {
            get { return m_signaler.Handle; }
        }

        public void Send(Command cmd)
        {
            bool ok;
            lock (m_sync)
            {
                m_cpipe.Write(ref cmd, false);
                ok = m_cpipe.Flush();
            }

            //if (LOG.isDebugEnabled())
            //    LOG.debug( "{} -> {} / {} {}", new Object[] { Thread.currentThread().getName(), cmd_, cmd_.arg , !ok});

            if (!ok)
            {
                m_signaler.Send();
            }
        }

        [CanBeNull]
        public Command Recv(int timeout)
        {
            Command cmd = null;
            
            //  Try to get the command straight away.
            if (m_active)
            {
                m_cpipe.Read(ref cmd);
                
                if (cmd != null)
                    return cmd;

                //  If there are no more commands available, switch into passive state.
                m_active = false;
                m_signaler.Recv();
            }


            //  Wait for signal from the command sender.
            bool rc = m_signaler.WaitEvent(timeout);
            if (!rc)
                return null;

            //  We've got the signal. Now we can switch into active state.
            m_active = true;

            //  Get a command.
            bool ok = m_cpipe.Read(ref cmd);
            Debug.Assert(ok);

            return cmd;
        }

        public void Close()
        {
            m_signaler.Close();
        }

        public override String ToString()
        {
            return base.ToString() + "[" + m_name + "]";
        }
    }
}
