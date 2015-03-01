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
using JetBrains.Annotations;
using NetMQ.zmq.Transports;

namespace NetMQ.zmq
{
    /// <summary>
    /// This is the base-class for all objects that participate in inter-thread communication.
    /// </summary>
    internal abstract class ZObject
    {
        /// <summary>
        /// This Context provides access to the global state.
        /// </summary>
        private readonly Ctx m_ctx;

        /// <summary>
        /// This is the thread-ID of the thread that this object belongs to.
        /// </summary>
        private readonly int m_threadId;

        protected ZObject([NotNull] Ctx ctx, int threadId)
        {
            m_ctx = ctx;
            m_threadId = threadId;
        }

        protected ZObject([NotNull] ZObject parent)
            : this(parent.m_ctx, parent.m_threadId)
        {}

        /// <summary>
        /// Get the id of the thread that this object belongs to.
        /// </summary>
        public int ThreadId
        {
            get { return m_threadId; }
        }

        /// <summary>
        /// Get the context that provides access to the global state.
        /// </summary>
        [NotNull]
        protected Ctx Ctx
        {
            get { return m_ctx; }
        }

        public void ProcessCommand([NotNull] Command cmd)
        {
            switch (cmd.CommandType)
            {
                case CommandType.ActivateRead:
                    ProcessActivateRead();
                    break;

                case CommandType.ActivateWrite:
                    ProcessActivateWrite((long)cmd.Arg);
                    break;

                case CommandType.Stop:
                    ProcessStop();
                    break;

                case CommandType.Plug:
                    ProcessPlug();
                    ProcessSeqnum();
                    break;

                case CommandType.Own:
                    ProcessOwn((Own)cmd.Arg);
                    ProcessSeqnum();
                    break;

                case CommandType.Attach:
                    ProcessAttach((IEngine)cmd.Arg);
                    ProcessSeqnum();
                    break;

                case CommandType.Bind:
                    ProcessBind((Pipe)cmd.Arg);
                    ProcessSeqnum();
                    break;

                case CommandType.Hiccup:
                    ProcessHiccup(cmd.Arg);
                    break;

                case CommandType.PipeTerm:
                    ProcessPipeTerm();
                    break;

                case CommandType.PipeTermAck:
                    ProcessPipeTermAck();
                    break;

                case CommandType.TermReq:
                    ProcessTermReq((Own)cmd.Arg);
                    break;

                case CommandType.Term:
                    ProcessTerm((int)cmd.Arg);
                    break;

                case CommandType.TermAck:
                    ProcessTermAck();
                    break;

                case CommandType.Reap:
                    ProcessReap((SocketBase)cmd.Arg);
                    break;

                case CommandType.Reaped:
                    ProcessReaped();
                    break;

                default:
                    throw new ArgumentException();
            }
        }

        protected bool RegisterEndpoint([NotNull] String addr, [NotNull] Ctx.Endpoint endpoint)
        {
            return m_ctx.RegisterEndpoint(addr, endpoint);
        }

        protected bool UnregisterEndpoint([NotNull] string addr, [NotNull] SocketBase socket)
        {
            return m_ctx.UnregisterEndpoint(addr, socket);
        }

        protected void UnregisterEndpoints([NotNull] SocketBase socket)
        {
            m_ctx.UnregisterEndpoints(socket);
        }

        [NotNull]
        protected Ctx.Endpoint FindEndpoint([NotNull] String addr)
        {
            return m_ctx.FindEndpoint(addr);
        }

        protected void DestroySocket([NotNull] SocketBase socket)
        {
            m_ctx.DestroySocket(socket);
        }

        /// <summary>
        /// Select and return the least loaded I/O thread.
        /// </summary>
        /// <param name="affinity"></param>
        /// <returns>the IOThread</returns>
        [CanBeNull]
        protected IOThread ChooseIOThread(long affinity)
        {
            return m_ctx.ChooseIOThread(affinity);
        }

        /// <summary>
        /// Send the Stop command.
        /// </summary>
        protected void SendStop()
        {
            // 'stop' command goes always from administrative thread to the current object. 
            m_ctx.SendCommand(m_threadId, new Command(this, CommandType.Stop));
        }

        /// <summary>
        /// Send the Plug command, incrementing the destinations sequence-number if incSeqnum is true.
        /// </summary>
        /// <param name="destination">the Own to send the command to</param>
        /// <param name="incSeqnum">a flag that dictates whether to increment the sequence-number on the destination (optional - defaults to false)</param>
        protected void SendPlug([NotNull] Own destination, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            SendCommand(new Command(destination, CommandType.Plug));
        }

        /// <summary>
        /// Send the Own command, and increment the sequence-number of the destination
        /// </summary>
        /// <param name="destination">the Own to send the command to</param>
        /// <param name="obj">the object to Own</param>
        protected void SendOwn([NotNull] Own destination, [NotNull] Own obj)
        {
            destination.IncSeqnum();
            SendCommand(new Command(destination, CommandType.Own, obj));
        }

        /// <summary>
        /// Send the Attach command
        /// </summary>
        /// <param name="destination">the Own to send the command to</param>
        /// <param name="engine"></param>
        /// <param name="incSeqnum"></param>
        protected void SendAttach([NotNull] SessionBase destination, [NotNull] IEngine engine, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            SendCommand(new Command(destination, CommandType.Attach, engine));
        }

        /// <summary>
        /// Send the Bind command
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="pipe"></param>
        /// <param name="incSeqnum"></param>
        protected void SendBind([NotNull] Own destination, [NotNull] Pipe pipe, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            SendCommand(new Command(destination, CommandType.Bind, pipe));
        }

        protected void SendActivateRead([NotNull] Pipe destination)
        {
            SendCommand(new Command(destination, CommandType.ActivateRead));
        }

        protected void SendActivateWrite([NotNull] Pipe destination, long msgsRead)
        {
            SendCommand(new Command(destination, CommandType.ActivateWrite, msgsRead));
        }

        protected void SendHiccup([NotNull] Pipe destination, [NotNull] Object pipe)
        {
            SendCommand(new Command(destination, CommandType.Hiccup, pipe));
        }

        protected void SendPipeTerm([NotNull] Pipe destination)
        {
            SendCommand(new Command(destination, CommandType.PipeTerm));
        }

        protected void SendPipeTermAck([NotNull] Pipe destination)
        {
            SendCommand(new Command(destination, CommandType.PipeTermAck));
        }

        protected void SendTermReq([NotNull] Own destination, [NotNull] Own obj)
        {
            SendCommand(new Command(destination, CommandType.TermReq, obj));
        }

        protected void SendTerm([NotNull] Own destination, int linger)
        {
            SendCommand(new Command(destination, CommandType.Term, linger));
        }

        protected void SendTermAck([NotNull] Own destination)
        {
            SendCommand(new Command(destination, CommandType.TermAck));
        }

        protected void SendReap([NotNull] SocketBase socket)
        {
            SendCommand(new Command(m_ctx.GetReaper(), CommandType.Reap, socket));
        }

        protected void SendReaped()
        {
            SendCommand(new Command(m_ctx.GetReaper(), CommandType.Reaped));
        }

        protected void SendDone()
        {
            // Use m_ctx.SendCommand directly as we have a null destination
            m_ctx.SendCommand(Ctx.TermTid, new Command(null, CommandType.Done));
        }

        protected virtual void ProcessStop()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessPlug()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessOwn([NotNull] Own obj)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessAttach([NotNull] IEngine engine)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessBind([NotNull] Pipe pipe)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessActivateRead()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessActivateWrite(long msgsRead)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessHiccup([NotNull] Object pipe)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessPipeTerm()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessPipeTermAck()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessTermReq([NotNull] Own obj)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessTerm(int linger)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessTermAck()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessReap([NotNull] SocketBase socket)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessReaped()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Special handler called after a command that requires a seqnum
        /// was processed. The implementation should catch up with its counter
        /// of processed commands here.
        /// </summary>
        protected virtual void ProcessSeqnum()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Send the given Command, on that commands Destination thread.
        /// </summary>
        /// <param name="cmd">the Command to send</param>
        private void SendCommand([NotNull] Command cmd)
        {
            m_ctx.SendCommand(cmd.Destination.ThreadId, cmd);
        }
    }
}