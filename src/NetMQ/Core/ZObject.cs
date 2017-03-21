/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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
using NetMQ.Core.Transports;

namespace NetMQ.Core
{
    /// <summary>
    /// This is the base-class for all objects that participate in inter-thread communication.
    /// </summary>
    internal abstract class ZObject
    {
        /// <summary>
        /// This Ctx is the context that provides access to the global state.
        /// </summary>
        private readonly Ctx m_ctx;

        /// <summary>
        /// This is the thread-ID of the thread that this object belongs to.
        /// </summary>
        private readonly int m_threadId;

        /// <summary>
        /// Create a new ZObject with the given context and thread-id.
        /// </summary>
        /// <param name="ctx">the context for the new ZObject to live within</param>
        /// <param name="threadId">the integer thread-id for the new ZObject to be associated with</param>
        protected ZObject([NotNull] Ctx ctx, int threadId)
        {
            m_ctx = ctx;
            m_threadId = threadId;
        }

        /// <summary>
        /// Create a new ZObject that has the same context and thread-id as the given parent-ZObject.
        /// </summary>
        /// <param name="parent">another ZObject that provides the context and thread-id for this one</param>
        protected ZObject([NotNull] ZObject parent)
            : this(parent.m_ctx, parent.m_threadId)
        {}

        /// <summary>
        /// Get the id of the thread that this object belongs to.
        /// </summary>
        public int ThreadId => m_threadId;

        /// <summary>
        /// Get the context that provides access to the global state.
        /// </summary>
        [NotNull]
        protected Ctx Ctx => m_ctx;

        protected bool RegisterEndpoint([NotNull] string addr, [NotNull] Ctx.Endpoint endpoint)
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

        /// <exception cref="EndpointNotFoundException">The given address was not found in the list of endpoints.</exception>
        [NotNull]
        protected Ctx.Endpoint FindEndpoint([NotNull] string addr)
        {
            return m_ctx.FindEndpoint(addr);
        }

        protected void DestroySocket([NotNull] SocketBase socket)
        {
            m_ctx.DestroySocket(socket);
        }

        /// <summary>
        /// Returns the <see cref="IOThread"/> that is the least busy at the moment.
        /// </summary>
        /// <paramref name="affinity">Which threads are eligible (0 = all).</paramref>
        /// <returns>The least busy thread, or <c>null</c> if none is available.</returns>
        [CanBeNull]
        protected IOThread ChooseIOThread(long affinity)
        {
            return m_ctx.ChooseIOThread(affinity);
        }

        #region Sending commands

        /// <summary>
        /// Send the Stop command.
        /// </summary>
        protected void SendStop()
        {
            // 'stop' command goes always from administrative thread to the current object.
            m_ctx.SendCommand(m_threadId, new Command(this, CommandType.Stop));
        }

        protected void SendForceStop()
        {
            m_ctx.SendCommand(m_threadId, new Command(this, CommandType.ForceStop));
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

        protected void SendHiccup([NotNull] Pipe destination, [NotNull] object pipe)
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

        /// <summary>
        /// For owned objects, asks the owner (<paramref name="destination"/>) to terminate <paramref name="obj"/>.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="obj"></param>
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

        /// <summary>
        /// Send a Done command to the Ctx itself (null destination).
        /// </summary>
        protected void SendDone()
        {
            // Use m_ctx.SendCommand directly as we have a null destination
            m_ctx.SendCommand(Ctx.TermTid, new Command(null, CommandType.Done));
        }

        /// <summary>
        /// Send the given Command, on that commands Destination thread.
        /// </summary>
        /// <param name="cmd">the Command to send</param>
        private void SendCommand([NotNull] Command cmd)
        {
            m_ctx.SendCommand(cmd.Destination.ThreadId, cmd);
        }

        #endregion

        #region Command processing

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

                case CommandType.ForceStop:
                    ProcessForceStop();
                    break;

                default:
                    throw new ArgumentException();
            }
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessStop()
        {
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessForceStop()
        {
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessPlug()
        {
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessOwn([NotNull] Own obj)
        {
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessAttach([NotNull] IEngine engine)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Process the bind command with the given pipe.
        /// </summary>
        /// <param name="pipe"></param>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessBind([NotNull] Pipe pipe)
        {
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessActivateRead()
        {
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessActivateWrite(long msgsRead)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This method would be called to assign the specified pipe as a replacement for the outbound pipe that was being used.
        /// This, is an abstract method that is to be overridden by subclasses to provide their own concrete implementation.
        /// </summary>
        /// <param name="pipe">the pipe to use for writing</param>
        /// <remarks>
        /// A "Hiccup" occurs when an outbound pipe experiences something like a transient disconnect or for whatever other reason
        /// is no longer available for writing to.
        /// </remarks>
        /// <exception cref="NotSupportedException">No supported on the ZObject class.</exception>
        protected virtual void ProcessHiccup([NotNull] object pipe)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Process the terminate-pipe command.
        /// </summary>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessPipeTerm()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Process the terminate-pipe acknowledgement command.
        /// </summary>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessPipeTermAck()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Process a termination-request command on the Own object.
        /// </summary>
        /// <param name="obj"></param>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessTermReq([NotNull] Own obj)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Process a termination request.
        /// </summary>
        /// <param name="linger">a time (in milliseconds) for this to linger before actually going away. -1 means infinite.</param>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessTerm(int linger)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Process the termination-acknowledgement command.
        /// </summary>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessTermAck()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessReap([NotNull] SocketBase socket)
        {
            // Overridden by Reaper
            throw new NotSupportedException();
        }

        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessReaped()
        {
            // Overridden by Reaper
            throw new NotSupportedException();
        }

        /// <summary>
        /// Special handler called after a command that requires a seqnum
        /// was processed. The implementation should catch up with its counter
        /// of processed commands here.
        /// </summary>
        /// <exception cref="NotSupportedException">Not supported on the ZObject class.</exception>
        protected virtual void ProcessSeqnum()
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}