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

//  Base class for all objects that participate in inter-thread
//  communication.
namespace NetMQ.zmq
{
    public abstract class ZObject
    {
        //  Context provides access to the global state.
        private readonly Ctx m_ctx;

        //  Thread ID of the thread the object belongs to.
        private readonly int m_threadId;

        protected ZObject(Ctx ctx, int threadId)
        {
            this.m_ctx = ctx;
            this.m_threadId = threadId;
        }

        protected ZObject(ZObject parent)
            : this(parent.m_ctx, parent.m_threadId)
        {
        }

        public int ThreadId { get { return m_threadId; } }

        protected Ctx Ctx { get { return m_ctx; } }

        public void ProcessCommand(Command cmd)
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

        protected void RegisterEndpoint(String addr, Ctx.Endpoint endpoint)
        {
            m_ctx.RegisterEndpoint(addr, endpoint);
        }

        protected void UnregisterEndpoint(string addr, SocketBase socket)
        {
            m_ctx.UnregisterEndpoint(addr, socket);
        }

        protected void UnregisterEndpoints(SocketBase socket)
        {
            m_ctx.UnregisterEndpoints(socket);
        }

        protected Ctx.Endpoint FindEndpoint(String addr)
        {
            return m_ctx.FindEndpoint(addr);
        }

        protected void DestroySocket(SocketBase socket)
        {
            m_ctx.DestroySocket(socket);
        }

        //  Chooses least loaded I/O thread.
        protected IOThread ChooseIOThread(long affinity)
        {
            return m_ctx.ChooseIOThread(affinity);
        }

        protected void SendStop()
        {
            //  'stop' command goes always from administrative thread to
            //  the current object. 
            Command cmd = new Command(this, CommandType.Stop);
            m_ctx.SendCommand(m_threadId, cmd);
        }


        protected void SendPlug(Own destination, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            Command cmd = new Command(destination, CommandType.Plug);
            SendCommand(cmd);
        }


        protected void SendOwn(Own destination, Own obj)
        {
            destination.IncSeqnum();
            Command cmd = new Command(destination, CommandType.Own, obj);
            SendCommand(cmd);
        }

        protected void SendAttach(SessionBase destination,
                                    IEngine engine, bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            Command cmd = new Command(destination, CommandType.Attach, engine);
            SendCommand(cmd);
        }


        protected void SendBind(Own destination, Pipe pipe,
                                  bool incSeqnum = true)
        {
            if (incSeqnum)
                destination.IncSeqnum();

            Command cmd = new Command(destination, CommandType.Bind, pipe);
            SendCommand(cmd);
        }


        protected void SendActivateRead(Pipe destination)
        {
            Command cmd = new Command(destination, CommandType.ActivateRead);
            SendCommand(cmd);
        }

        protected void SendActivateWrite(Pipe destination,
                                            long msgsRead)
        {
            Command cmd = new Command(destination, CommandType.ActivateWrite, msgsRead);
            SendCommand(cmd);
        }

        protected void SendHiccup(Pipe destination, Object pipe)
        {
            Command cmd = new Command(destination, CommandType.Hiccup, pipe);
            SendCommand(cmd);
        }


        protected void SendPipeTerm(Pipe destination)
        {
            Command cmd = new Command(destination, CommandType.PipeTerm);
            SendCommand(cmd);
        }


        protected void SendPipeTermAck(Pipe destination)
        {
            Command cmd = new Command(destination, CommandType.PipeTermAck);
            SendCommand(cmd);
        }


        protected void SendTermReq(Own destination,
                                      Own object_)
        {
            Command cmd = new Command(destination, CommandType.TermReq, object_);
            SendCommand(cmd);
        }


        protected void SendTerm(Own destination, int linger)
        {
            Command cmd = new Command(destination, CommandType.Term, linger);
            SendCommand(cmd);

        }


        protected void SendTermAck(Own destination)
        {
            Command cmd = new Command(destination, CommandType.TermAck);
            SendCommand(cmd);
        }

        protected void SendReap(SocketBase socket)
        {
            Command cmd = new Command(m_ctx.GetReaper(), CommandType.Reap, socket);
            SendCommand(cmd);
        }


        protected void SendReaped()
        {
            Command cmd = new Command(m_ctx.GetReaper(), CommandType.Reaped);
            SendCommand(cmd);
        }



        protected void SendDone()
        {
            Command cmd = new Command(null, CommandType.Done);
            m_ctx.SendCommand(Ctx.TermTid, cmd);
        }


        protected virtual void ProcessStop()
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessPlug()
        {
            throw new NotSupportedException();
        }


        protected virtual void ProcessOwn(Own obj)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessAttach(IEngine engine)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessBind(Pipe pipe)
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

        protected virtual void ProcessHiccup(Object pipe)
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

        protected virtual void ProcessTermReq(Own obj)
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

        protected virtual void ProcessReap(SocketBase socket)
        {
            throw new NotSupportedException();
        }

        protected virtual void ProcessReaped()
        {
            throw new NotSupportedException();
        }

        //  Special handler called after a command that requires a seqnum
        //  was processed. The implementation should catch up with its counter
        //  of processed commands here.
        protected virtual void ProcessSeqnum()
        {
            throw new NotSupportedException();
        }


        private void SendCommand(Command cmd)
        {
            m_ctx.SendCommand(cmd.Destination.ThreadId, cmd);
        }
    }
}
