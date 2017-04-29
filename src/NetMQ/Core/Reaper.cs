/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011-2015 Other contributors as noted in the AUTHORS file

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
using JetBrains.Annotations;

namespace NetMQ.Core
{
    /// <summary>
    /// Class Reaper is a ZObject and implements IPollEvents.
    /// The Reaper is dedicated toward handling socket shutdown asynchronously and cleanly.
    /// By passing this task off to the Reaper, the message-queueing subsystem can terminate immediately.
    /// </summary>
    internal class Reaper : ZObject, IPollEvents
    {
        /// <summary>
        /// Reaper thread accesses incoming commands via this mailbox.
        /// </summary>
        [NotNull] private readonly Mailbox m_mailbox;

        /// <summary>
        /// This is a Socket, used as the handle associated with the mailbox's file descriptor.
        /// </summary>
        [NotNull] private readonly Socket m_mailboxHandle;

        /// <summary>
        /// I/O multiplexing is performed using a poller object.
        /// </summary>
        [NotNull] private readonly Utils.Poller m_poller;

        /// <summary>
        /// Number of sockets being reaped at the moment.
        /// These are the reason for having a reaper: to take over the task of terminating these sockets.
        /// </summary>
        private int m_sockets;

        /// <summary>
        /// If true, we were already asked to terminate.
        /// </summary>
        private volatile bool m_terminating;

        /// <summary>
        /// Create a new Reaper with the given thread-id.
        /// This will have a new Poller with the name "reaper-" + thread-id, and a Mailbox of that same name.
        /// </summary>
        /// <param name="ctx">the Ctx for this to be in</param>
        /// <param name="threadId">an integer id to give to the thread this will live on</param>
        public Reaper([NotNull] Ctx ctx, int threadId)
            : base(ctx, threadId)
        {
            m_sockets = 0;
            m_terminating = false;

            string name = "reaper-" + threadId;
            m_poller = new Utils.Poller(name);

            m_mailbox = new Mailbox(name);

            m_mailboxHandle = m_mailbox.Handle;
            m_poller.AddHandle(m_mailboxHandle, this);
            m_poller.SetPollIn(m_mailboxHandle);
        }

        /// <summary>
        /// Release any contained resources - by destroying the poller and closing the mailbox.
        /// </summary>
        public void Destroy()
        {
            m_poller.Destroy();
            m_mailbox.Close();
        }

        /// <summary>
        /// Get the Mailbox that this Reaper uses for communication with the rest of the message-queueing subsystem.
        /// </summary>
        [NotNull]
        public Mailbox Mailbox => m_mailbox;

        /// <summary>
        /// Start the contained Poller to begin polling.
        /// </summary>
        public void Start()
        {
            m_poller.Start();
        }

        /// <summary>
        /// Issue the Stop command to this Reaper object.
        /// </summary>
        public void Stop()
        {
            if (!m_terminating)
                SendStop();
        }

        public void ForceStop()
        {
            SendForceStop();
        }

        /// <summary>
        /// Handle input-ready events, by receiving and processing any commands
        /// that are waiting in the mailbox.
        /// </summary>
        public void InEvent()
        {
            while (true)
            {
                // Get the next command. If there is none, exit.
                if (!m_mailbox.TryRecv(0, out Command command))
                    break;

                // Process the command.
                command.Destination.ProcessCommand(command);
            }
        }

        /// <summary>
        /// This method normally is for handling output-ready events, which don't apply here.
        /// </summary>
        /// <exception cref="NotSupportedException">You must not call OutEvent on a Reaper.</exception>
        public void OutEvent()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This would be called when a timer expires - however in this event NotSupportedException would be thrown.
        /// </summary>
        /// <param name="id">an integer used to identify the timer</param>
        /// <exception cref="NotSupportedException">You must not call TimerEvent on a Reaper.</exception>
        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Respond to the Stop command by signaling the polling-loop to terminate,
        /// and if there're no sockets left to reap - stop the poller.
        /// </summary>
        protected override void ProcessStop()
        {
            m_terminating = true;

            // If there are no sockets being reaped finish immediately.
            if (m_sockets == 0)
            {
                SendDone();
                m_poller.RemoveHandle(m_mailboxHandle);
                m_poller.Stop();
            }
        }

        protected override void ProcessForceStop()
        {
            m_terminating = true;
            SendDone();
            m_poller.RemoveHandle(m_mailboxHandle);
            m_poller.Stop();
        }

        /// <summary>
        /// Add the given socket to the list to be reaped (terminated).
        /// </summary>
        /// <param name="socket">the socket to add to the list for termination</param>
        protected override void ProcessReap(SocketBase socket)
        {
            // Add the socket to the poller.
            socket.StartReaping(m_poller);

            ++m_sockets;
        }

        /// <summary>
        /// Respond to having one of the sockets that are marked for reaping, - finished being reaped,
        /// and if there are none left - send the Done command and stop the poller.
        /// </summary>
        protected override void ProcessReaped()
        {
            --m_sockets;

            // If reaped was already asked to terminate and there are no more sockets,
            // finish immediately.
            if (m_sockets == 0 && m_terminating)
            {
                SendDone();
                m_poller.RemoveHandle(m_mailboxHandle);
                m_poller.Stop();
            }
        }
    }
}
