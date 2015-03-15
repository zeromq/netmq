/*  
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011 Other contributors as noted in the AUTHORS file
          
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
using System.Net.Sockets;

namespace NetMQ.zmq
{
    internal class Reaper : ZObject, IPollEvents
    {
        /// <summary>
        /// Reaper thread accesses incoming commands via this mailbox.
        /// </summary>
        private readonly Mailbox m_mailbox;

        /// <summary>
        /// This is a Socket, used as the handle associated with the mailbox's file descriptor.
        /// </summary>
        private readonly Socket m_mailboxHandle;

        /// <summary>
        /// I/O multiplexing is performed using a poller object.
        /// </summary>
        private readonly Utils.Poller m_poller;

        /// <summary>
        /// Number of sockets being reaped at the moment.
        /// </summary>
        private int m_sockets;

        /// <summary>
        /// If true, we were already asked to terminate.
        /// </summary>
        private volatile bool m_terminating;

        public Reaper(Ctx ctx, int threadId)
            : base(ctx, threadId)
        {
            m_sockets = 0;
            m_terminating = false;
            
            string name = "reaper-" + threadId;
            m_poller = new Utils.Poller(name);

            m_mailbox = new Mailbox(name);

            m_mailboxHandle = m_mailbox.Handle;
            m_poller.AddHandle(m_mailboxHandle, this);
            m_poller.SetPollin(m_mailboxHandle);
        }

        public void Destroy()
        {
            m_poller.Destroy();
            m_mailbox.Close();
        }

        public Mailbox Mailbox
        {
            get { return m_mailbox; }
        }

        public void Start()
        {
            m_poller.Start();
        }

        public void Stop()
        {
            if (!m_terminating)
                SendStop();
        }

        public void InEvent()
        {
            while (true)
            {
                //  Get the next command. If there is none, exit.
                Command cmd = m_mailbox.Recv(0);
                if (cmd == null)
                    break;

                //  Process the command.
                cmd.Destination.ProcessCommand(cmd);
            }
        }

        public void OutEvent()
        {
            throw new NotSupportedException();
        }

        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }

        protected override void ProcessStop()
        {
            m_terminating = true;

            //  If there are no sockets being reaped finish immediately.
            if (m_sockets == 0)
            {
                SendDone();
                m_poller.RemoveHandle(m_mailboxHandle);
                m_poller.Stop();
            }
        }

        protected override void ProcessReap(SocketBase socket)
        {
            //  Add the socket to the poller.
            socket.StartReaping(m_poller);

            ++m_sockets;
        }

        protected override void ProcessReaped()
        {
            --m_sockets;

            //  If reaped was already asked to terminate and there are no more sockets,
            //  finish immediately.
            if (m_sockets == 0 && m_terminating)
            {
                SendDone();
                m_poller.RemoveHandle(m_mailboxHandle);
                m_poller.Stop();
            }
        }
    }
}
