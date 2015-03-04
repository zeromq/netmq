/*      
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011 VMware, Inc.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;
using NetMQ.zmq.Patterns;
using NetMQ.zmq.Transports.Ipc;
using NetMQ.zmq.Transports.PGM;
using NetMQ.zmq.Transports.Tcp;
using NetMQ.zmq.Utils;
using TcpListener = NetMQ.zmq.Transports.Tcp.TcpListener;

namespace NetMQ.zmq
{
    internal abstract class SocketBase : Own, IPollEvents, Pipe.IPipeEvents
    {
        [NotNull] private readonly Dictionary<String, Own> m_endpoints;

        [NotNull] private readonly Dictionary<string, Pipe> m_inprocs;

        private bool m_disposed;

        //  If true, associated context was already terminated.
        private bool m_ctxTerminated;

        //  If true, object should have been already destroyed. However,
        //  destruction is delayed while we unwind the stack to the point
        //  where it doesn't intersect the object being destroyed.
        private bool m_destroyed;

        //  Socket's mailbox object.
        [NotNull] private readonly Mailbox m_mailbox;

        //  List of attached pipes.        
        [NotNull] private readonly List<Pipe> m_pipes;

        //  Reaper's poller and handle of this socket within it.
        private Utils.Poller m_poller;
        private Socket m_handle;

        //  Timestamp of when commands were processed the last time.
        private long m_lastTsc;

        //  Number of messages received since last command processing.
        private int m_ticks;

        //  True if the last message received had MORE flag set.
        private bool m_rcvMore;

        // Monitor socket
        private SocketBase m_monitorSocket;

        // Bitmask of events being monitored
        private SocketEvent m_monitorEvents;

        // The tcp port that was bound to, if any
        private int m_port;

        protected SocketBase([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId)
        {
            m_disposed = false;
            m_ctxTerminated = false;
            m_destroyed = false;
            m_lastTsc = 0;
            m_ticks = 0;
            m_rcvMore = false;
            m_monitorSocket = null;
            m_monitorEvents = 0;

            m_options.SocketId = socketId;

            m_endpoints = new Dictionary<string, Own>();
            m_inprocs = new Dictionary<string, Pipe>();
            m_pipes = new List<Pipe>();

            m_mailbox = new Mailbox("socket-" + socketId);
        }

        //  Concrete algorithms for the x- methods are to be defined by
        //  individual socket types.
        protected abstract void XAttachPipe([NotNull] Pipe pipe, bool icanhasall);
        protected abstract void XTerminated([NotNull] Pipe pipe);

        /// <summary>
        /// Throw exception if socket is disposed
        /// </summary>  
        public void CheckDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        /// <summary>
        /// Throw a TerminatingException if the message-queueing system has started terminating.
        /// </summary>
        public void CheckContextTerminated()
        {
            if (m_ctxTerminated)
            {
                throw new TerminatingException(innerException: null, message: "CheckContextTerminated - yes, is terminated.");
            }
        }

        //  Create a socket of a specified type.
        [NotNull]
        public static SocketBase Create(ZmqSocketType type, [NotNull] Ctx parent, int threadId, int socketId)
        {
            switch (type)
            {
                case ZmqSocketType.Pair:
                    return new Pair(parent, threadId, socketId);
                case ZmqSocketType.Pub:
                    return new Pub(parent, threadId, socketId);
                case ZmqSocketType.Sub:
                    return new Sub(parent, threadId, socketId);
                case ZmqSocketType.Req:
                    return new Req(parent, threadId, socketId);
                case ZmqSocketType.Rep:
                    return new Rep(parent, threadId, socketId);
                case ZmqSocketType.Dealer:
                    return new Dealer(parent, threadId, socketId);
                case ZmqSocketType.Router:
                    return new Router(parent, threadId, socketId);
                case ZmqSocketType.Pull:
                    return new Pull(parent, threadId, socketId);
                case ZmqSocketType.Push:
                    return new Push(parent, threadId, socketId);
                case ZmqSocketType.Xpub:
                    return new XPub(parent, threadId, socketId);
                case ZmqSocketType.Xsub:
                    return new XSub(parent, threadId, socketId);
                case ZmqSocketType.Stream:
                    return new Stream(parent, threadId, socketId);
                default:
                    throw new InvalidException("SocketBase.Create called with invalid type of " + type);
            }
        }

        /// <summary>
        /// This simply calls StopMonitor, and then verifies that the destroyed-flag is set.
        /// </summary>
        public override void Destroy()
        {
            StopMonitor();

            Debug.Assert(m_destroyed);
        }

        /// <summary>
        /// Returns the mailbox associated with this socket.
        /// </summary>
        [NotNull]
        public Mailbox Mailbox
        {
            get { return m_mailbox; }
        }

        /// <summary>
        /// Interrupt blocking call if the socket is stuck in one.
        /// This function can be called from a different thread!
        /// </summary>
        public void Stop()
        {
            //  Called by ctx when it is terminated (zmq_term).
            //  'stop' command is sent from the threads that called zmq_term to
            //  the thread owning the socket. This way, blocking call in the
            //  owner thread can be interrupted.
            SendStop();
        }

        /// <summary>
        /// Check whether transport protocol, as specified in connect or
        /// bind, is available and compatible with the socket type.
        /// </summary>
        private void CheckProtocol([NotNull] string protocol)
        {
            //  First check out whether the protocol is something we are aware of.
            if (!protocol.Equals(Address.InProcProtocol) &&
                !protocol.Equals(Address.IpcProtocol) && !protocol.Equals(Address.TcpProtocol) &&
                !protocol.Equals(Address.PgmProtocol) && !protocol.Equals(Address.EpgmProtocol))
            {
#if DEBUG
                String xMsg = String.Format("SocketBase.CheckProtocol({0}), protocol is invalid.", protocol);
                throw new ProtocolNotSupportedException(xMsg);
#else
                throw new ProtocolNotSupportedException();
#endif
            }

            //  Check whether socket type and transport protocol match.
            //  Specifically, multicast protocols can't be combined with
            //  bi-directional messaging patterns (socket types).
            if ((protocol.Equals(Address.PgmProtocol) || protocol.Equals(Address.EpgmProtocol)) &&
                m_options.SocketType != ZmqSocketType.Pub && m_options.SocketType != ZmqSocketType.Sub &&
                m_options.SocketType != ZmqSocketType.Xpub && m_options.SocketType != ZmqSocketType.Xsub)
            {
#if DEBUG
                String xMsg = String.Format("SocketBase.CheckProtocol({0}), socket type {1} and protocol do not match.", protocol, m_options.SocketType);
                throw new ProtocolNotSupportedException(xMsg);
#else
                throw new ProtocolNotSupportedException();
#endif
            }

            //  Protocol is available.
        }


        /// <summary>
        /// Register the pipe with this socket.
        /// </summary>
        private void AttachPipe([NotNull] Pipe pipe, bool icanhasall = false)
        {
            //  First, register the pipe so that we can terminate it later on.

            pipe.SetEventSink(this);
            m_pipes.Add(pipe);

            //  Let the derived socket type know about new pipe.
            XAttachPipe(pipe, icanhasall);

            //  If the socket is already being closed, ask any new pipes to terminate
            //  straight away.
            if (IsTerminating)
            {
                RegisterTermAcks(1);
                pipe.Terminate(false);
            }
        }

        public void SetSocketOption(ZmqSocketOptions option, Object optval)
        {
            CheckContextTerminated();

            //  First, check whether specific socket type overloads the option.
            if (!XSetSocketOption(option, optval))
            {
                //  If the socket type doesn't support the option, pass it to
                //  the generic option parser.
                m_options.SetSocketOption(option, optval);
            }
        }

        public int GetSocketOption(ZmqSocketOptions option)
        {
            CheckContextTerminated();

            if (option == ZmqSocketOptions.ReceiveMore)
            {
                return m_rcvMore ? 1 : 0;
            }
            if (option == ZmqSocketOptions.Events)
            {
                try
                {
                    ProcessCommands(0, false);
                }
                catch (TerminatingException)
                {
                    return -1;
                }

                PollEvents val = 0;
                if (HasOut())
                    val |= PollEvents.PollOut;
                if (HasIn())
                    val |= PollEvents.PollIn;
                return (int)val;
            }

            return (int)GetSocketOptionX(option);
        }

        public Object GetSocketOptionX(ZmqSocketOptions option)
        {
            CheckContextTerminated();

            if (option == ZmqSocketOptions.ReceiveMore)
            {
                return m_rcvMore;
            }

            if (option == ZmqSocketOptions.Handle)
            {
                return m_mailbox.Handle;
            }

            if (option == ZmqSocketOptions.Events)
            {
                try
                {
                    ProcessCommands(0, false);
                }
                catch (TerminatingException)
                {
                    return -1;
                }

                PollEvents val = 0;
                if (HasOut())
                    val |= PollEvents.PollOut;
                if (HasIn())
                    val |= PollEvents.PollIn;
                return val;
            }
            //  If the socket type doesn't support the option, pass it to
            //  the generic option parser.
            return m_options.GetSocketOption(option);
        }

        public void Bind([NotNull] string addr)
        {
            CheckContextTerminated();

            //  Process pending commands, if any.
            ProcessCommands(0, false);

            string protocol;
            string address;

            DecodeAddress(addr, out address, out protocol);

            CheckProtocol(protocol);

            if (protocol.Equals(Address.InProcProtocol))
            {
                var endpoint = new Ctx.Endpoint(this, m_options);

                bool addressRegistered = RegisterEndpoint(addr, endpoint);

                if (!addressRegistered)
                {
                    string xMsg = String.Format("Cannot bind address ( {0} ) - already in use.", addr);
                    throw new AddressAlreadyInUseException(xMsg);
                }

                // Save last endpoint URI
                m_options.LastEndpoint = addr;

                return;
            }
            if ((protocol.Equals(Address.PgmProtocol) || protocol.Equals(Address.EpgmProtocol)) && (
                m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub))
            {
                //  For convenience's sake, bind can be used interchangeable with
                //  connect for PGM and EPGM transports.
                Connect(addr);
                return;
            }

            //  Remaining transports require to be run in an I/O thread, so at this
            //  point we'll choose one.
            IOThread ioThread = ChooseIOThread(m_options.Affinity);
            if (ioThread == null)
            {
                throw NetMQException.Create(ErrorCode.EmptyThread);
            }

            if (protocol.Equals(Address.TcpProtocol))
            {
                var listener = new TcpListener(ioThread, this, m_options);

                try
                {
                    listener.SetAddress(address);
                    m_port = listener.Port;

                    // Recreate the address string (localhost:1234) in case the port was system-assigned
                    var host = address.Substring(0, address.IndexOf(':'));
                    addr = "tcp://" + host + ":" + m_port;
                }
                catch (NetMQException ex)
                {
                    listener.Destroy();
                    EventBindFailed(addr, ex.ErrorCode);

                    throw;
                }

                // Save last endpoint URI
                m_options.LastEndpoint = listener.Address;

                AddEndpoint(addr, listener);

                return;
            }

            if (protocol.Equals(Address.PgmProtocol) || protocol.Equals(Address.EpgmProtocol))
            {
                var listener = new PgmListener(ioThread, this, m_options);

                try
                {
                    listener.Init(address);
                }
                catch (NetMQException ex)
                {
                    listener.Destroy();
                    EventBindFailed(addr, ex.ErrorCode);

                    throw;
                }

                m_options.LastEndpoint = addr;

                AddEndpoint(addr, listener);

                return;
            }

            if (protocol.Equals(Address.IpcProtocol))
            {
                var listener = new IpcListener(ioThread, this, m_options);

                try
                {
                    listener.SetAddress(address);
                    m_port = listener.Port;
                }
                catch (NetMQException ex)
                {
                    listener.Destroy();
                    EventBindFailed(addr, ex.ErrorCode);

                    throw;
                }

                // Save last endpoint URI
                m_options.LastEndpoint = listener.Address;

                AddEndpoint(addr, listener);
                return;
            }

            Debug.Assert(false);
            throw new FaultException(String.Format("SocketBase.Bind({0}) failure.", addr));
        }

        public int BindRandomPort([NotNull] string addr)
        {
            string address, protocol;

            DecodeAddress(addr, out address, out protocol);

            if (!protocol.Equals(Address.TcpProtocol))
                throw new ProtocolNotSupportedException(String.Format("In SocketBase.BindRandomPort({0}), protocol should be tcp.", addr));

            Bind(addr + ":0");
            return m_port;
        }

        public void Connect([NotNull] string addr)
        {
            CheckContextTerminated();

            //  Process pending commands, if any.
            ProcessCommands(0, false);

            string address;
            string protocol;
            DecodeAddress(addr, out address, out protocol);

            CheckProtocol(protocol);

            if (protocol.Equals(Address.InProcProtocol))
            {
                //  TODO: inproc connect is specific with respect to creating pipes
                //  as there's no 'reconnect' functionality implemented. Once that
                //  is in place we should follow generic pipe creation algorithm.

                //  Find the peer endpoint.
                Ctx.Endpoint peer = FindEndpoint(addr);

                // The total HWM for an inproc connection should be the sum of
                // the binder's HWM and the connector's HWM.
                int sndhwm;
                int rcvhwm;
                if (m_options.SendHighWatermark == 0 || peer.Options.ReceiveHighWatermark == 0)
                    sndhwm = 0;
                else
                    sndhwm = m_options.SendHighWatermark + peer.Options.ReceiveHighWatermark;
                if (m_options.ReceiveHighWatermark == 0 || peer.Options.SendHighWatermark == 0)
                    rcvhwm = 0;
                else
                    rcvhwm = m_options.ReceiveHighWatermark + peer.Options.SendHighWatermark;

                //  Create a bi-directional pipe to connect the peers.
                ZObject[] parents = { this, peer.Socket };
                int[] highWaterMarks = { sndhwm, rcvhwm };
                bool[] delays = { m_options.DelayOnDisconnect, m_options.DelayOnClose };
                Pipe[] pipes = Pipe.PipePair(parents, highWaterMarks, delays);

                //  Attach local end of the pipe to this socket object.
                AttachPipe(pipes[0]);

                //  If required, send the identity of the peer to the local socket.
                if (peer.Options.RecvIdentity)
                {
                    var id = new Msg();
                    id.InitPool(peer.Options.IdentitySize);
                    id.Put(peer.Options.Identity, 0, peer.Options.IdentitySize);
                    id.SetFlags(MsgFlags.Identity);
                    bool written = pipes[0].Write(ref id);
                    Debug.Assert(written);
                    pipes[0].Flush();
                }

                //  If required, send the identity of the local socket to the peer.
                if (m_options.RecvIdentity)
                {
                    var id = new Msg();
                    id.InitPool(m_options.IdentitySize);
                    id.Put(m_options.Identity, 0, m_options.IdentitySize);
                    id.SetFlags(MsgFlags.Identity);
                    bool written = pipes[1].Write(ref id);
                    Debug.Assert(written);
                    pipes[1].Flush();
                }

                //  Attach remote end of the pipe to the peer socket. Note that peer's
                //  seqnum was incremented in find_endpoint function. We don't need it
                //  increased here.
                SendBind(peer.Socket, pipes[1], false);

                // Save last endpoint URI
                m_options.LastEndpoint = addr;

                // remember inproc connections for disconnect
                m_inprocs.Add(addr, pipes[0]);

                return;
            }

            //  Choose the I/O thread to run the session in.
            IOThread ioThread = ChooseIOThread(m_options.Affinity);
            if (ioThread == null)
            {
                throw NetMQException.Create("Empty IO Thread", ErrorCode.EmptyThread);
            }
            var paddr = new Address(protocol, address);

            //  Resolve address (if needed by the protocol)
            if (protocol.Equals(Address.TcpProtocol))
            {
                paddr.Resolved = (new TcpAddress());
                paddr.Resolved.Resolve(
                    address, m_options.IPv4Only);
            }
            else if (protocol.Equals(Address.IpcProtocol))
            {
                paddr.Resolved = (new IpcAddress());
                paddr.Resolved.Resolve(address, true);
            }
            else if (protocol.Equals(Address.PgmProtocol) || protocol.Equals(Address.EpgmProtocol))
            {
                if (m_options.SocketType == ZmqSocketType.Sub || m_options.SocketType == ZmqSocketType.Xsub)
                {
                    Bind(addr);
                    return;
                }

                paddr.Resolved = new PgmAddress();
                paddr.Resolved.Resolve(address, m_options.IPv4Only);
            }

            //  Create session.
            SessionBase session = SessionBase.Create(ioThread, true, this, m_options, paddr);
            Debug.Assert(session != null);

            //  PGM does not support subscription forwarding; ask for all data to be
            //  sent to this pipe.
            bool icanhasall = protocol.Equals(Address.PgmProtocol) || protocol.Equals(Address.EpgmProtocol);

            if (!m_options.DelayAttachOnConnect || icanhasall)
            {
                //  Create a bi-directional pipe.
                ZObject[] parents = { this, session };
                int[] hwms = { m_options.SendHighWatermark, m_options.ReceiveHighWatermark };
                bool[] delays = { m_options.DelayOnDisconnect, m_options.DelayOnClose };
                Pipe[] pipes = Pipe.PipePair(parents, hwms, delays);

                //  Attach local end of the pipe to the socket object.
                AttachPipe(pipes[0], icanhasall);

                //  Attach remote end of the pipe to the session object later on.
                session.AttachPipe(pipes[1]);
            }

            // Save last endpoint URI
            m_options.LastEndpoint = paddr.ToString();

            AddEndpoint(addr, session);
        }

        private static void DecodeAddress([NotNull] string addr, out string address, out string protocol)
        {
            const string protocolDelimeter = "://";
            int protocolDelimeterIndex = addr.IndexOf(protocolDelimeter, StringComparison.Ordinal);

            protocol = addr.Substring(0, protocolDelimeterIndex);
            address = addr.Substring(protocolDelimeterIndex + protocolDelimeter.Length);
        }

        /// <summary>
        /// Creates new endpoint ID and adds the endpoint to the map.
        /// </summary>
        private void AddEndpoint([NotNull] string addr, [NotNull] Own endpoint)
        {
            //  Activate the session. Make it a child of this socket.
            LaunchChild(endpoint);
            m_endpoints[addr] = endpoint;
        }

        public void TermEndpoint([NotNull] string addr)
        {
            CheckContextTerminated();

            //  Check whether endpoint address passed to the function is valid.
            if (addr == null)
            {
                throw new InvalidException("TermEndpoint must not be called with a null addr.");
            }

            //  Process pending commands, if any, since there could be pending unprocessed process_own()'s
            //  (from launch_child() for example) we're asked to terminate now.
            ProcessCommands(0, false);

            string protocol;
            string address;

            DecodeAddress(addr, out address, out protocol);

            CheckProtocol(protocol);

            if (protocol == Address.InProcProtocol)
            {
                bool found = UnregisterEndpoint(addr, this);

                if (!found)
                {
                    Pipe pipe;

                    if (m_inprocs.TryGetValue(addr, out pipe))
                    {
                        pipe.Terminate(true);
                        m_inprocs.Remove(addr);
                    }
                    else
                    {
                        throw new EndpointNotFoundException("Endpoint was not found and cannot be disconnected");
                    }
                }
            }
            else
            {
                Own endpoint;

                if (m_endpoints.TryGetValue(addr, out endpoint))
                {
                    TermChild(endpoint);

                    m_endpoints.Remove(addr);
                }
                else
                {
                    throw new EndpointNotFoundException("Endpoint was not found and cannot be disconnected");
                }
            }
        }

        /// <summary>
        /// Transmit the given Msg across the message-queueing system.
        /// If the msg fails to immediately send, then - if DontWait is specified and no SendTimeout was set
        /// then throw an AgainException.
        /// </summary>
        /// <param name="msg">the Msg to transmit</param>
        /// <param name="flags">a SendReceiveOptions: either don't specify DontWait, or set a timeout</param>
        public void Send(ref Msg msg, SendReceiveOptions flags)
        {
            CheckContextTerminated();

            //  Check whether message passed to the function is valid.
            if (!msg.IsInitialised)
                throw new FaultException("SocketBase.Send passed an uninitialised Msg.");

            //  Process pending commands, if any.
            ProcessCommands(0, true);

            //  Clear any user-visible flags that are set on the message.
            msg.ResetFlags(MsgFlags.More);

            //  At this point we impose the flags on the message.
            if ((flags & SendReceiveOptions.SendMore) > 0)
                msg.SetFlags(MsgFlags.More);

            //  Try to send the message.
            bool isMessageSent = XSend(ref msg, flags);

            if (isMessageSent)
                return;

            //  In case of non-blocking send we'll simply propagate
            //  the error - including EAGAIN - up the stack.
            bool isDontWaitSet = (flags & SendReceiveOptions.DontWait) > 0;
            if (isDontWaitSet || m_options.SendTimeout == 0)
            {
#if DEBUG
                string xMsg;
                if (isDontWaitSet && m_options.SendTimeout == 0)
                    xMsg = "SocketBase.Send failed, and DontWait is true AND SendTimeout is 0.";
                else if (isDontWaitSet)
                    xMsg = "SocketBase.Send failed and DontWait is specified.";
                else
                    xMsg = "SocketBase.Send failed and no SendTimeout is specified.";
                throw new AgainException(innerException: null, message: xMsg);
#else
                throw new AgainException(innerException: null, message: "SocketBase.Send failed");
#endif
            }

            //  Compute the time when the timeout should occur.
            //  If the timeout is infinite, don't care. 
            int timeout = m_options.SendTimeout;
            long end = timeout < 0 ? 0 : (Clock.NowMs() + timeout);

            //  Oops, we couldn't send the message. Wait for the next
            //  command, process it and try to send the message again.
            //  If timeout is reached in the meantime, return EAGAIN.
            while (true)
            {
                ProcessCommands(timeout, false);

                isMessageSent = XSend(ref msg, flags);
                
                if (isMessageSent)
                    break;

                if (timeout <= 0)
                    continue;

                timeout = (int)(end - Clock.NowMs());
                    
                if (timeout <= 0)
                    throw new AgainException(innerException: null, message: "SocketBase.Send failed and timeout <= 0");
            }
        }

        public void Recv(ref Msg msg, SendReceiveOptions flags)
        {
            CheckContextTerminated();

            //  Check whether message passed to the function is valid.
            if (!msg.IsInitialised)
                throw new FaultException("SocketBase.Recv passed an uninitialised Msg.");

            //  Get the message.
            bool isMessageAvailable = XRecv(flags, ref msg);

            //  Once every inbound_poll_rate messages check for signals and process
            //  incoming commands. This happens only if we are not polling altogether
            //  because there are messages available all the time. If poll occurs,
            //  ticks is set to zero and thus we avoid this code.
            //
            //  Note that 'recv' uses different command throttling algorithm (the one
            //  described above) from the one used by 'send'. This is because counting
            //  ticks is more efficient than doing RDTSC all the time.
            if (++m_ticks == Config.InboundPollRate)
            {
                ProcessCommands(0, false);
                m_ticks = 0;
            }

            //  If we have the message, return immediately.
            if (isMessageAvailable)
            {
                ExtractFlags(ref msg);
                return;
            }

            //  If the message cannot be fetched immediately, there are two scenarios.
            //  For non-blocking recv, commands are processed in case there's an
            //  activate_reader command already waiting int a command pipe.
            //  If it's not, return EAGAIN.
            bool isDontWaitSet = (flags & SendReceiveOptions.DontWait) > 0;
            if (isDontWaitSet || m_options.ReceiveTimeout == 0)
            {
                ProcessCommands(0, false);
                m_ticks = 0;

                isMessageAvailable = XRecv(flags, ref msg);
                if (!isMessageAvailable)
                {
#if DEBUG
                    string xMsg;
                    if (isDontWaitSet && m_options.ReceiveTimeout == 0)
                    {
                        xMsg = "SocketBase.Recv failed: No message is available, DontWait is set AND SendTimeout is 0.";
                    }
                    else if (isDontWaitSet)
                    {
                        xMsg = "SocketBase.Recv failed: No message is available, and DontWait is set.";
                    }
                    else
                    {
                        xMsg = "SocketBase.Recv failed: No message is available, and there is no ReceiveTimeout specified.";
                    }
                    throw new AgainException(innerException: null, message: xMsg);
#else
                    throw new AgainException(innerException: null, message: "SocketBase.Recv failed");
#endif
                }

                ExtractFlags(ref msg);
                return;
            }

            //  Compute the time when the timeout should occur.
            //  If the timeout is infinite, don't care. 
            int timeout = m_options.ReceiveTimeout;
            long end = timeout < 0 ? 0 : (Clock.NowMs() + timeout);

            //  In blocking scenario, commands are processed over and over again until
            //  we are able to fetch a message.
            bool block = (m_ticks != 0);
            while (true)
            {
                ProcessCommands(block ? timeout : 0, false);

                isMessageAvailable = XRecv(flags, ref msg);
                if (isMessageAvailable)
                {
                    m_ticks = 0;
                    break;
                }

                block = true;
                if (timeout > 0)
                {
                    timeout = (int)(end - Clock.NowMs());
                    if (timeout <= 0)
                    {
                        throw new AgainException(innerException: null, message: "SocketBase.Recv failed and timeout <= 0");
                    }
                }
            }

            ExtractFlags(ref msg);
        }

        public void Close()
        {
            //  Mark the socket as disposed
            m_disposed = true;

            //  Transfer the ownership of the socket from this application thread
            //  to the reaper thread which will take care of the rest of shutdown
            //  process.
            SendReap(this);
        }

        /// <summary>
        /// These functions are used by the polling mechanism to determine
        /// which events are to be reported from this socket.
        /// </summary>
        public bool HasIn()
        {
            return XHasIn();
        }

        public bool HasOut()
        {
            return XHasOut();
        }

        /// <summary>
        /// Using this function reaper thread ask the socket to register with
        /// its poller.
        /// </summary>
        internal void StartReaping([NotNull] Utils.Poller poller)
        {
            //  Plug the socket to the reaper thread.
            m_poller = poller;
            m_handle = m_mailbox.Handle;
            m_poller.AddHandle(m_handle, this);
            m_poller.SetPollin(m_handle);

            //  Initialise the termination and check whether it can be deallocated
            //  immediately.
            Terminate();
            CheckDestroy();
        }

        /// <summary>
        /// Processes commands sent to this socket (if any). If timeout is -1,
        /// returns only after at least one command was processed.
        /// If throttle argument is true, commands are processed at most once
        /// in a predefined time period.
        /// </summary>
        private void ProcessCommands(int timeout, bool throttle)
        {
            Command cmd;
            if (timeout != 0)
            {
                //  If we are asked to wait, simply ask mailbox to wait.
                cmd = m_mailbox.Recv(timeout);
            }
            else
            {
                //  If we are asked not to wait, check whether we haven't processed
                //  commands recently, so that we can throttle the new commands.

                //  Get the CPU's tick counter. If 0, the counter is not available.								
                long tsc = Clock.Rdtsc();

                //  Optimised version of command processing - it doesn't have to check
                //  for incoming commands each time. It does so only if certain time
                //  elapsed since last command processing. Command delay varies
                //  depending on CPU speed: It's ~1ms on 3GHz CPU, ~2ms on 1.5GHz CPU
                //  etc. The optimisation makes sense only on platforms where getting
                //  a timestamp is a very cheap operation (tens of nanoseconds).
                if (tsc != 0 && throttle)
                {
                    //  Check whether TSC haven't jumped backwards (in case of migration
                    //  between CPU cores) and whether certain time have elapsed since
                    //  last command processing. If it didn't do nothing.
                    if (tsc >= m_lastTsc && tsc - m_lastTsc <= Config.MaxCommandDelay)
                        return;
                    m_lastTsc = tsc;
                }

                //  Check whether there are any commands pending for this thread.
                cmd = m_mailbox.Recv(0);
            }

            //  Process all the commands available at the moment.
            while (true)
            {
                if (cmd == null)
                    break;

                cmd.Destination.ProcessCommand(cmd);
                cmd = m_mailbox.Recv(0);
            }

            CheckContextTerminated();
        }

        protected override void ProcessStop()
        {
            //  Here, someone have called zmq_term while the socket was still alive.
            //  We'll remember the fact so that any blocking call is interrupted and any
            //  further attempt to use the socket will return ETERM. The user is still
            //  responsible for calling zmq_close on the socket though!
            StopMonitor();
            m_ctxTerminated = true;
        }

        protected override void ProcessBind(Pipe pipe)
        {
            AttachPipe(pipe);
        }

        protected override void ProcessTerm(int linger)
        {
            //  Unregister all inproc endpoints associated with this socket.
            //  Doing this we make sure that no new pipes from other sockets (inproc)
            //  will be initiated.
            UnregisterEndpoints(this);

            //  Ask all attached pipes to terminate.
            for (int i = 0; i != m_pipes.Count; ++i)
                m_pipes[i].Terminate(false);
            RegisterTermAcks(m_pipes.Count);

            //  Continue the termination process immediately.
            base.ProcessTerm(linger);
        }

        /// <summary>
        /// Delay actual destruction of the socket.
        /// </summary>
        protected override void ProcessDestroy()
        {
            m_destroyed = true;
        }

        /// <summary>
        /// The default implementation assumes there are no specific socket
        /// options for the particular socket type. If not so, overload this
        /// method.
        /// </summary>
        protected virtual bool XSetSocketOption(ZmqSocketOptions option, [CanBeNull] Object optval)
        {
            return false;
        }

        protected virtual bool XHasOut()
        {
            return false;
        }

        protected virtual bool XSend(ref Msg msg, SendReceiveOptions flags)
        {
            throw new NotSupportedException("Must Override");
        }

        protected virtual bool XHasIn()
        {
            return false;
        }

        protected virtual bool XRecv(SendReceiveOptions flags, ref Msg msg)
        {
            throw new NotSupportedException("Must Override");
        }

        protected virtual void XReadActivated([NotNull] Pipe pipe)
        {
            throw new NotSupportedException("Must Override");
        }

        protected virtual void XWriteActivated([NotNull] Pipe pipe)
        {
            throw new NotSupportedException("Must Override");
        }

        protected virtual void XHiccuped([NotNull] Pipe pipe)
        {
            throw new NotSupportedException("Must override");
        }

        public virtual void InEvent()
        {
            //  This function is invoked only once the socket is running in the context
            //  of the reaper thread. Process any commands from other threads/sockets
            //  that may be available at the moment. Ultimately, the socket will
            //  be destroyed.

            try
            {
                ProcessCommands(0, false);
            }
            finally
            {
                CheckDestroy();
            }
        }

        public virtual void OutEvent()
        {
            throw new NotSupportedException();
        }

        public virtual void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// To be called after processing commands or invoking any command
        /// handlers explicitly. If required, it will deallocate the socket.
        /// </summary>
        private void CheckDestroy()
        {
            //  If the object was already marked as destroyed, finish the deallocation.
            if (m_destroyed)
            {
                //  Remove the socket from the reaper's poller.
                m_poller.RemoveHandle(m_handle);
                //  Remove the socket from the context.
                DestroySocket(this);

                //  Notify the reaper about the fact.
                SendReaped();

                //  Deallocate.
                base.ProcessDestroy();
            }
        }

        public void ReadActivated(Pipe pipe)
        {
            XReadActivated(pipe);
        }

        public void WriteActivated(Pipe pipe)
        {
            XWriteActivated(pipe);
        }

        public void Hiccuped(Pipe pipe)
        {
            if (m_options.DelayAttachOnConnect)
                pipe.Terminate(false);
            else
                // Notify derived sockets of the hiccup
                XHiccuped(pipe);
        }

        public void Terminated(Pipe pipe)
        {
            //  Notify the specific socket type about the pipe termination.
            XTerminated(pipe);

            // Remove pipe from inproc pipes
            var pipesToDelete = m_inprocs.Where(i => i.Value == pipe).Select(i => i.Key).ToArray();
            foreach (var addr in pipesToDelete)
            {
                m_inprocs.Remove(addr);
            }

            //  Remove the pipe from the list of attached pipes and confirm its
            //  termination if we are already shutting down.
            m_pipes.Remove(pipe);
            if (IsTerminating)
                UnregisterTermAck();
        }

        /// <summary>
        /// Moves the flags from the message to local variables,
        /// to be later retrieved by getsockopt.
        /// </summary>
        private void ExtractFlags(ref Msg msg)
        {
            //  Test whether IDENTITY flag is valid for this socket type.
            Debug.Assert(!msg.IsIdentity || m_options.RecvIdentity);

            //  Remove MORE flag.
            m_rcvMore = msg.HasMore;
        }

        public void Monitor([CanBeNull] string addr, SocketEvent events)
        {
            CheckContextTerminated();

            // Support de-registering monitoring endpoints as well
            if (addr == null)
            {
                StopMonitor();
                return;
            }

            string address;
            string protocol;
            DecodeAddress(addr, out address, out protocol);

            CheckProtocol(protocol);

            // Event notification only supported over inproc://
            if (!protocol.Equals(Address.InProcProtocol))
            {
                throw new ProtocolNotSupportedException(String.Format("In SocketBase.Monitor({0},), protocol must be inproc", addr));
            }

            // Register events to monitor
            m_monitorEvents = events;

            m_monitorSocket = Ctx.CreateSocket(ZmqSocketType.Pair);
            if (m_monitorSocket == null)
                throw new FaultException("In SocketBase.Monitor, Ctx.CreateSocket(ZmqSocketType.Pair) failed.");

            // Never block context termination on pending event messages
            const int linger = 0;

            try
            {
                m_monitorSocket.SetSocketOption(ZmqSocketOptions.Linger, linger);
            }
            catch (NetMQException)
            {
                StopMonitor();
                throw;
            }

            // Spawn the monitor socket endpoint
            try
            {
                m_monitorSocket.Bind(addr);
            }
            catch (NetMQException)
            {
                StopMonitor();
                throw;
            }
        }

        public void EventConnected([NotNull] String addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvent.Connected) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.Connected, addr, ch));
        }

        public void EventConnectDelayed([NotNull] String addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvent.ConnectDelayed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.ConnectDelayed, addr, errno));
        }

        public void EventConnectRetried([NotNull] String addr, int interval)
        {
            if ((m_monitorEvents & SocketEvent.ConnectRetried) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.ConnectRetried, addr, interval));
        }

        public void EventListening([NotNull] String addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvent.Listening) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.Listening, addr, ch));
        }

        public void EventBindFailed([NotNull] String addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvent.BindFailed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.BindFailed, addr, errno));
        }

        public void EventAccepted([NotNull] String addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvent.Accepted) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.Accepted, addr, ch));
        }

        public void EventAcceptFailed([NotNull] String addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvent.AcceptFailed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.AcceptFailed, addr, errno));
        }

        public void EventClosed([NotNull] String addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvent.Closed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.Closed, addr, ch));
        }

        public void EventCloseFailed([NotNull] String addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvent.CloseFailed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.CloseFailed, addr, errno));
        }

        public void EventDisconnected([NotNull] String addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvent.Disconnected) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvent.Disconnected, addr, ch));
        }

        private void MonitorEvent([NotNull] MonitorEvent monitorEvent)
        {
            if (m_monitorSocket == null)
                return;

            monitorEvent.Write(m_monitorSocket);
        }

        /// <summary>
        /// If there is a monitor-socket, close it and set monitor-events to 0.
        /// </summary>
        private void StopMonitor()
        {
            if (m_monitorSocket != null)
            {
                m_monitorSocket.Close();
                m_monitorSocket = null;
                m_monitorEvents = 0;
            }
        }

        public override string ToString()
        {
            return base.ToString() + "[" + m_options.SocketId + "]";
        }

        /// <summary>
        /// Get the Socket (Handle) - which is actually the Handle of the contained mailbox.
        /// </summary>
        [NotNull]
        public Socket Handle
        {
            get { return m_mailbox.Handle; }
        }

        /// <summary>
        /// Return a short bit of text that denotes the SocketType of this socket.
        /// </summary>
        /// <returns>a short type-string such as PAIR, PUB, OR UNKNOWN</returns>
        [NotNull]
        public string GetTypeString()
        {
            switch (m_options.SocketType)
            {
                case ZmqSocketType.Pair:
                    return "PAIR";
                case ZmqSocketType.Pub:
                    return "PUB";
                case ZmqSocketType.Sub:
                    return "SUB";
                case ZmqSocketType.Req:
                    return "REQ";
                case ZmqSocketType.Rep:
                    return "REP";
                case ZmqSocketType.Dealer:
                    return "DEALER";
                case ZmqSocketType.Router:
                    return "ROUTER";
                case ZmqSocketType.Pull:
                    return "PULL";
                case ZmqSocketType.Push:
                    return "PUSH";
                default:
                    return "UNKNOWN";
            }
        }
    }
}
