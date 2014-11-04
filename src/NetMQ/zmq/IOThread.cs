/*
    Copyright (c) 2009-2011 250bpm s.r.o.
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

namespace NetMQ.zmq
{
    public class IOThread : ZObject, IMailboxEvent
    {

        //  I/O thread accesses incoming commands via this mailbox.
        private readonly IOThreadMailbox m_mailbox;
        
        //  I/O multiplexing is performed using a poller object.
        private readonly Proactor m_proactor;

        readonly String m_name;

        public IOThread(Ctx ctx, int threadId)
            : base(ctx, threadId)
        {

            m_name = "iothread-" + threadId;
            m_proactor = new Proactor(m_name);            

            m_mailbox = new IOThreadMailbox(m_name, m_proactor, this);                        
        }

        public Proactor Proactor
        {
            get { return m_proactor; }
        }

        public void Start()
        {
            m_proactor.Start();
        }

        public void Destroy()
        {
            m_proactor.Destroy();
            m_mailbox.Close();
        }
        public void Stop()
        {
            SendStop();
        }

        public IMailbox Mailbox
        {
            get
            {
                return m_mailbox;
            }
        }

        public int Load
        {
            get { return m_proactor.Load; }
        }                  

        //public Poller GetPoller()
        //{
        //    Debug.Assert(m_poller != null);
        //    return m_poller;
        //}

        protected override void ProcessStop()
        {            
            m_proactor.Stop();
        }

        public void Ready()
        {
            while (true)
            {
                //  Get the next command. If there is none, exit.
                Command cmd = m_mailbox.Recv();
                if (cmd == null)
                    break;

                //  Process the command.
                cmd.Destination.ProcessCommand(cmd);
            }
        }
    }
}
