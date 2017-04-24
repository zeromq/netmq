/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011 VMware, Inc.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;
using NetMQ.Core.Patterns;
using NetMQ.Core.Transports.Ipc;
using NetMQ.Core.Transports.Pgm;
using NetMQ.Core.Transports.Tcp;
using NetMQ.Core.Utils;
using TcpListener = NetMQ.Core.Transports.Tcp.TcpListener;

namespace NetMQ.Core
{
    internal abstract class SocketBase : Own, IPollEvents, Pipe.IPipeEvents
    {
        private class Endpoint
        {
            public Endpoint(Own own, Pipe pipe)
            {
                Own = own;
                Pipe = pipe;
            }

            public Own Own { get; }
            public Pipe Pipe { get; }
        }

        [NotNull] private readonly Dictionary<string, Endpoint> m_endpoints = new Dictionary<string, Endpoint>();

        [NotNull] private readonly Dictionary<string, Pipe> m_inprocs = new Dictionary<string, Pipe>();

        private bool m_disposed;

        /// <summary>If true, associated context was already terminated.</summary>
        private bool m_isStopped;

        /// <summary>
        /// If true, object should have been already destroyed. However,
        /// destruction is delayed while we unwind the stack to the point
        /// where it doesn't intersect the object being destroyed.
        /// </summary>
        private bool m_destroyed;

        /// <summary>Socket's mailbox object.</summary>
        [NotNull] private readonly Mailbox m_mailbox;

        /// <summary>List of attached pipes.</summary>
        [NotNull] private readonly List<Pipe> m_pipes = new List<Pipe>();

        /// <summary>Reaper's poller.</summary>
        private Poller m_poller;

        /// <summary>The handle of this socket within the reaper's poller.</summary>
        private Socket m_handle;

        /// <summary>Timestamp of when commands were processed the last time.</summary>
        private long m_lastTsc;

        /// <summary>Number of messages received since last command processing.</summary>
        private int m_ticks;

        /// <summary>True if the last message received had MORE flag set.</summary>
        private bool m_rcvMore;

        /// <summary>Monitor socket.</summary>
        private SocketBase m_monitorSocket;

        /// <summary>Bitmask of events being monitored.</summary>
        private SocketEvents m_monitorEvents;

        /// <summary>The tcp port that was bound to, if any.</summary>
        private int m_port;

        /// <summary>
        /// Create a new SocketBase within the given Ctx, with the specified thread-id and socket-id.
        /// </summary>
        /// <param name="parent">the Ctx context that this socket will live within</param>
        /// <param name="threadId">the id of the thread upon which this socket will execute</param>
        /// <param name="socketId">the integer id for the new socket</param>
        protected SocketBase([NotNull] Ctx parent, int threadId, int socketId)
            : base(parent, threadId)
        {
            m_options.SocketId = socketId;
            m_mailbox = new Mailbox("socket-" + socketId);
        }

        // Note: Concrete algorithms for the x- methods are to be defined by
        // individual socket types.

        /// <summary>
        /// Abstract method for attaching a given pipe to this socket.
        /// The concrete implementations are defined by the individual socket types.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">if true - subscribe to all data on the pipe</param>
        protected abstract void XAttachPipe([NotNull] Pipe pipe, bool icanhasall);

        /// <summary>
        /// Abstract method that gets called to signal that the given pipe is to be removed from this socket.
        /// The concrete implementations of SocketBase override this to provide their own implementation
        /// of how to terminate the pipe.
        /// </summary>
        /// <param name="pipe">the Pipe that is being removed</param>
        protected abstract void XTerminated([NotNull] Pipe pipe);

        /// <summary>Throw <see cref="ObjectDisposedException"/> if this socket is already disposed.</summary>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        public void CheckDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Throw <see cref="TerminatingException"/> if the message-queueing system has started terminating.
        /// </summary>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        private void CheckContextTerminated()
        {
            if (m_isStopped)
                throw new TerminatingException(innerException: null, message: "CheckContextTerminated - yes, is terminated.");
        }

        /// <summary>
        /// Create a socket of a specified type.
        /// </summary>
        /// <param name="type">a ZmqSocketType specifying the type of socket to create</param>
        /// <param name="parent">the parent context</param>
        /// <param name="threadId">the thread for this new socket to run on</param>
        /// <param name="socketId">an integer id for this socket</param>
        /// <exception cref="InvalidException">The socket type must be valid.</exception>
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
        /// Destroy this socket - which means to stop monitoring the queue for messages.
        /// This simply calls StopMonitor, and then verifies that the destroyed-flag is set.
        /// </summary>
        public override void Destroy()
        {
            StopMonitor();

            Debug.Assert(m_destroyed);
        }

        /// <summary>
        /// Return the Mailbox associated with this socket.
        /// </summary>
        [NotNull]
        public Mailbox Mailbox => m_mailbox;

        /// <summary>
        /// Interrupt a blocking call if the socket is stuck in one.
        /// This function can be called from a different thread!
        /// </summary>
        public void Stop()
        {
            // Called by ctx when it is terminated (zmq_term).
            //  'stop' command is sent from the threads that called zmq_term to
            // the thread owning the socket. This way, blocking call in the
            // owner thread can be interrupted.
            SendStop();
        }

        /// <summary>
        /// Check whether the transport protocol, as specified in connect or
        /// bind, is available and compatible with the socket type.
        /// </summary>
        /// <exception cref="ProtocolNotSupportedException">the specified protocol is not supported</exception>
        /// <exception cref="ProtocolNotSupportedException">the socket type and protocol do not match</exception>
        /// <remarks>
        /// The supported protocols are "inproc", "ipc", "tcp", "pgm", and "epgm".
        /// If the protocol is either "pgm" or "epgm", then this socket must be of type Pub, Sub, XPub, or XSub.
        /// </remarks>
        private void CheckProtocol([NotNull] string protocol)
        {
            switch (protocol)
            {
                case Address.InProcProtocol:
                case Address.IpcProtocol:
                case Address.TcpProtocol:
                    // All is well
                    break;
                case Address.PgmProtocol:
                case Address.EpgmProtocol:
                    // Check whether socket type and transport protocol match.
                    // Specifically, multicast protocols can't be combined with
                    // bi-directional messaging patterns (socket types).
                    switch (m_options.SocketType)
                    {
                        case ZmqSocketType.Pub:
                        case ZmqSocketType.Sub:
                        case ZmqSocketType.Xpub:
                        case ZmqSocketType.Xsub:
                            // All is well
                            break;
                        default:
                            throw new ProtocolNotSupportedException(
                                "Multicast protocols are not supported by socket type: " + m_options.SocketType);
                    }
                    break;
                default:
                    throw new ProtocolNotSupportedException("Invalid protocol: " + protocol);
            }
        }

        /// <summary>
        /// Register the given pipe with this socket.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        /// <param name="icanhasall">if true - subscribe to all data on the pipe (optional - default is false)</param>
        private void AttachPipe([NotNull] Pipe pipe, bool icanhasall = false)
        {
            // First, register the pipe so that we can terminate it later on.

            pipe.SetEventSink(this);
            m_pipes.Add(pipe);

            // Let the derived socket type know about new pipe.
            XAttachPipe(pipe, icanhasall);

            // If the socket is already being closed, ask any new pipes to terminate
            // straight away.
            if (IsTerminating)
            {
                RegisterTermAcks(1);
                pipe.Terminate(false);
            }
        }

        /// <summary>
        /// Set the specified socket option.
        /// </summary>
        /// <param name="option">which option to set</param>
        /// <param name="optionValue">the value to set the option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public void SetSocketOption(ZmqSocketOption option, object optionValue)
        {
            CheckContextTerminated();

            // First, check whether specific socket type overloads the option.
            if (!XSetSocketOption(option, optionValue))
            {
                // If the socket type doesn't support the option, pass it to
                // the generic option parser.
                m_options.SetSocketOption(option, optionValue);
            }
        }

        /// <summary>
        /// Return the integer-value of the specified option.
        /// </summary>
        /// <param name="option">which option to get</param>
        /// <returns>the value of the specified option, or -1 if error</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <remarks>
        /// If the ReceiveMore option is specified, then 1 is returned if it is true, 0 if it is false.
        /// If the Events option is specified, then process any outstanding commands, and return -1 if that throws a TerminatingException.
        ///     then return an integer that is the bitwise-OR of the PollEvents.PollOut and PollEvents.PollIn flags.
        /// Otherwise, cast the specified option value to an integer and return it.
        /// </remarks>
        public int GetSocketOption(ZmqSocketOption option)
        {
            CheckContextTerminated();

            if (option == ZmqSocketOption.ReceiveMore)
            {
                return m_rcvMore ? 1 : 0;
            }
            if (option == ZmqSocketOption.Events)
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

        /// <summary>
        /// Return the value of the specified option as an Object.
        /// </summary>
        /// <param name="option">which option to get</param>
        /// <returns>the value of the option</returns>
        /// <exception cref="TerminatingException">The socket has already been stopped.</exception>
        /// <remarks>
        /// If the Handle option is specified, then return the handle of the contained mailbox.
        /// If the Events option is specified, then process any outstanding commands, and return -1 if that throws a TerminatingException.
        ///     then return a PollEvents that is the bitwise-OR of the PollEvents.PollOut and PollEvents.PollIn flags.
        /// </remarks>
        public object GetSocketOptionX(ZmqSocketOption option)
        {
            CheckContextTerminated();

            if (option == ZmqSocketOption.ReceiveMore)
            {
                return m_rcvMore;
            }

            if (option == ZmqSocketOption.Handle)
            {
                return m_mailbox.Handle;
            }

            if (option == ZmqSocketOption.Events)
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
            // If the socket type doesn't support the option, pass it to
            // the generic option parser.
            return m_options.GetSocketOption(option);
        }

        /// <summary>
        /// Bind this socket to the given address.
        /// </summary>
        /// <param name="addr">a string denoting the endpoint-address to bind to</param>
        /// <exception cref="AddressAlreadyInUseException">the address specified to bind to must not be already in use</exception>
        /// <exception cref="ArgumentException">The requested protocol is not supported.</exception>
        /// <exception cref="FaultException">the socket bind failed</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener encountered an
        /// error during initialisation.</exception>
        /// <exception cref="ProtocolNotSupportedException">the specified protocol is not supported</exception>
        /// <exception cref="ProtocolNotSupportedException">the socket type and protocol do not match</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <remarks>
        /// The supported protocols are "inproc", "ipc", "tcp", "pgm", and "epgm".
        /// If the protocol is either "pgm" or "epgm", then this socket must be of type Pub, Sub, XPub, or XSub.
        /// If the protocol is "inproc", you cannot bind to the same address more than once.
        /// </remarks>
        public void Bind([NotNull] string addr)
        {
            CheckContextTerminated();

            // Process pending commands, if any.
            ProcessCommands(0, false);


            DecodeAddress(addr, out string address, out string protocol);

            CheckProtocol(protocol);

            switch (protocol)
            {
                case Address.InProcProtocol:
                    {
                        var endpoint = new Ctx.Endpoint(this, m_options);
                        bool addressRegistered = RegisterEndpoint(addr, endpoint);

                        if (!addressRegistered)
                            throw new AddressAlreadyInUseException($"Cannot bind address ( {addr} ) - already in use.");

                        m_options.LastEndpoint = addr;
                        return;
                    }
                case Address.PgmProtocol:
                case Address.EpgmProtocol:
                    {
                        if (m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub)
                        {
                            // For convenience's sake, bind can be used interchangeable with
                            // connect for PGM and EPGM transports.
                            Connect(addr);
                            return;
                        }
                        break;
                    }
            }

            // Remaining transports require to be run in an I/O thread, so at this
            // point we'll choose one.
            var ioThread = ChooseIOThread(m_options.Affinity);

            if (ioThread == null)
                throw NetMQException.Create(ErrorCode.EmptyThread);

            switch (protocol)
            {
                case Address.TcpProtocol:
                    {
                        var listener = new TcpListener(ioThread, this, m_options);

                        try
                        {
                            listener.SetAddress(address);
                            m_port = listener.Port;

                            // Recreate the address string (localhost:1234) in case the port was system-assigned
                            addr = $"tcp://{address.Substring(0, address.IndexOf(':'))}:{m_port}";
                        }
                        catch (NetMQException ex)
                        {
                            listener.Destroy();
                            EventBindFailed(addr, ex.ErrorCode);
                            throw;
                        }

                        m_options.LastEndpoint = listener.Address;
                        AddEndpoint(addr, listener, null);
                        break;
                    }
                case Address.PgmProtocol:
                case Address.EpgmProtocol:
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
                        AddEndpoint(addr, listener, null);
                        break;
                    }
                case Address.IpcProtocol:
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

                        m_options.LastEndpoint = listener.Address;
                        AddEndpoint(addr, listener,null);
                        break;
                    }
                default:
                    {
                        throw new ArgumentException($"Address {addr} has unsupported protocol: {protocol}", nameof(addr));
                    }
            }
        }

        /// <summary>Bind the specified TCP address to an available port, assigned by the operating system.</summary>
        /// <param name="addr">a string denoting the endpoint to bind to</param>
        /// <returns>the port-number that was bound to</returns>
        /// <exception cref="ProtocolNotSupportedException"><paramref name="addr"/> uses a protocol other than TCP.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener errored during
        /// initialisation.</exception>
        /// <exception cref="FaultException">the socket bind failed</exception>
        public int BindRandomPort([NotNull] string addr)
        {
            DecodeAddress(addr, out string address, out string protocol);

            if (protocol != Address.TcpProtocol)
                throw new ProtocolNotSupportedException("Address must use the TCP protocol.");

            Bind(addr + ":0");
            return m_port;
        }

        /// <summary>
        /// Connect this socket to the given address.
        /// </summary>
        /// <param name="addr">a string denoting the endpoint to connect to</param>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found.</exception>
        /// <exception cref="ProtocolNotSupportedException">the specified protocol is not supported</exception>
        /// <exception cref="ProtocolNotSupportedException">the socket type and protocol do not match</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <remarks>
        /// The supported protocols are "inproc", "ipc", "tcp", "pgm", and "epgm".
        /// If the protocol is either "pgm" or "epgm", then this socket must be of type Pub, Sub, XPub, or XSub.
        /// </remarks>
        /// <exception cref="EndpointNotFoundException">The given address was not found in the list of endpoints.</exception>
        public void Connect([NotNull] string addr)
        {
            CheckContextTerminated();

            // Process pending commands, if any.
            ProcessCommands(0, false);

            DecodeAddress(addr, out string address, out string protocol);

            CheckProtocol(protocol);

            if (protocol == Address.InProcProtocol)
            {
                // TODO: inproc connect is specific with respect to creating pipes
                // as there's no 'reconnect' functionality implemented. Once that
                // is in place we should follow generic pipe creation algorithm.

                // Find the peer endpoint.
                Ctx.Endpoint peer = FindEndpoint(addr);

                // The total HWM for an inproc connection should be the sum of
                // the binder's HWM and the connector's HWM.
                var sndhwm = m_options.SendHighWatermark != 0 && peer.Options.ReceiveHighWatermark != 0
                    ? m_options.SendHighWatermark + peer.Options.ReceiveHighWatermark
                    : 0;

                var rcvhwm = m_options.ReceiveHighWatermark != 0 && peer.Options.SendHighWatermark != 0
                    ? m_options.ReceiveHighWatermark + peer.Options.SendHighWatermark
                    : 0;

                // The total LWM for an inproc connection should be the sum of
                // the binder's LWM and the connector's LWM.
                int sndlwm = m_options.SendLowWatermark != 0 && peer.Options.ReceiveLowWatermark != 0
                    ? m_options.SendLowWatermark + peer.Options.ReceiveLowWatermark
                    : 0;

                int rcvlwm = m_options.ReceiveLowWatermark != 0 && peer.Options.SendLowWatermark != 0
                    ? m_options.ReceiveLowWatermark + peer.Options.SendLowWatermark
                    : 0;

                // Create a bi-directional pipe to connect the peers.
                ZObject[] parents = { this, peer.Socket };
                int[] highWaterMarks = { sndhwm, rcvhwm };
                int[] lowWaterMarks = { sndlwm, rcvlwm };
                bool[] delays = { m_options.DelayOnDisconnect, m_options.DelayOnClose };
                Pipe[] pipes = Pipe.PipePair(parents, highWaterMarks, lowWaterMarks, delays);

                // Attach local end of the pipe to this socket object.
                AttachPipe(pipes[0]);

                // If required, send the identity of the local socket to the peer.
                if (peer.Options.RecvIdentity)
                {
                    var id = new Msg();
                    id.InitPool(m_options.IdentitySize);
                    id.Put(m_options.Identity, 0, m_options.IdentitySize);
                    id.SetFlags(MsgFlags.Identity);
                    bool written = pipes[0].Write(ref id);
                    Debug.Assert(written);
                    pipes[0].Flush();
                }

                // If required, send the identity of the peer to the local socket.
                if (m_options.RecvIdentity)
                {
                    var id = new Msg();
                    id.InitPool(peer.Options.IdentitySize);
                    id.Put(peer.Options.Identity, 0, peer.Options.IdentitySize);
                    id.SetFlags(MsgFlags.Identity);
                    bool written = pipes[1].Write(ref id);
                    Debug.Assert(written);
                    pipes[1].Flush();
                }

                // Attach remote end of the pipe to the peer socket. Note that peer's
                // seqnum was incremented in find_endpoint function. We don't need it
                // increased here.
                SendBind(peer.Socket, pipes[1], false);

                // Save last endpoint URI
                m_options.LastEndpoint = addr;

                // remember inproc connections for disconnect
                m_inprocs.Add(addr, pipes[0]);

                return;
            }

            // Choose the I/O thread to run the session in.
            var ioThread = ChooseIOThread(m_options.Affinity);

            if (ioThread == null)
                throw NetMQException.Create(ErrorCode.EmptyThread);

            var paddr = new Address(protocol, address);

            // Resolve address (if needed by the protocol)
            switch (protocol)
            {
                case Address.TcpProtocol:
                    {
                        paddr.Resolved = (new TcpAddress());
                        paddr.Resolved.Resolve(address, m_options.IPv4Only);
                        break;
                    }
                case Address.IpcProtocol:
                    {
                        paddr.Resolved = (new IpcAddress());
                        paddr.Resolved.Resolve(address, true);
                        break;
                    }
                case Address.PgmProtocol:
                case Address.EpgmProtocol:
                    {
                        if (m_options.SocketType == ZmqSocketType.Sub || m_options.SocketType == ZmqSocketType.Xsub)
                        {
                            Bind(addr);
                            return;
                        }
                        paddr.Resolved = new PgmAddress();
                        paddr.Resolved.Resolve(address, m_options.IPv4Only);
                        break;
                    }
            }

            // Create session.
            SessionBase session = SessionBase.Create(ioThread, true, this, m_options, paddr);
            Debug.Assert(session != null);

            // PGM does not support subscription forwarding; ask for all data to be
            // sent to this pipe.
            bool icanhasall = protocol == Address.PgmProtocol || protocol == Address.EpgmProtocol;
            Pipe newPipe = null;

            if (!m_options.DelayAttachOnConnect || icanhasall)
            {
                // Create a bi-directional pipe.
                ZObject[] parents = { this, session };
                int[] hwms = { m_options.SendHighWatermark, m_options.ReceiveHighWatermark };
                int[] lwms = { m_options.SendLowWatermark, m_options.ReceiveLowWatermark };
                bool[] delays = { m_options.DelayOnDisconnect, m_options.DelayOnClose };
                Pipe[] pipes = Pipe.PipePair(parents, hwms, lwms, delays);

                // Attach local end of the pipe to the socket object.
                AttachPipe(pipes[0], icanhasall);
                newPipe = pipes[0];

                // Attach remote end of the pipe to the session object later on.
                session.AttachPipe(pipes[1]);
            }

            // Save last endpoint URI
            m_options.LastEndpoint = paddr.ToString();

            AddEndpoint(addr, session, newPipe);
        }

        /// <summary>
        /// Given a string containing an endpoint address like "tcp://127.0.0.1:5555,
        /// break-it-down into the address part ("127.0.0.1:5555") and the protocol part ("tcp").
        /// </summary>
        /// <param name="addr">a string denoting the endpoint, to take the parts from</param>
        /// <param name="address">the IP-address portion of the end-point address</param>
        /// <param name="protocol">the protocol portion of the end-point address (such as "tcp")</param>
        private static void DecodeAddress([NotNull] string addr, out string address, out string protocol)
        {
            const string protocolDelimeter = "://";
            int protocolDelimeterIndex = addr.IndexOf(protocolDelimeter, StringComparison.Ordinal);

            protocol = addr.Substring(0, protocolDelimeterIndex);
            address = addr.Substring(protocolDelimeterIndex + protocolDelimeter.Length);
        }

        /// <summary>
        /// Take ownership of the given <paramref name="endpoint"/> and register it against the given <paramref name="address"/>.
        /// </summary>
        private void AddEndpoint([NotNull] string address, [NotNull] Own endpoint, Pipe pipe)
        {
            // Activate the session. Make it a child of this socket.
            LaunchChild(endpoint);
            m_endpoints[address] = new Endpoint(endpoint, pipe);
        }

        /// <summary>
        /// Disconnect from the given endpoint.
        /// </summary>
        /// <param name="addr">the endpoint to disconnect from</param>
        /// <exception cref="ArgumentNullException"><paramref name="addr"/> is <c>null</c>.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        /// <exception cref="ProtocolNotSupportedException">The specified protocol must be valid.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public void TermEndpoint([NotNull] string addr)
        {
            CheckContextTerminated();

            // Check whether endpoint address passed to the function is valid.
            if (addr == null)
                throw new ArgumentNullException(nameof(addr));

            // Process pending commands, if any, since there could be pending unprocessed process_own()'s
            //  (from launch_child() for example) we're asked to terminate now.
            ProcessCommands(0, false);


            DecodeAddress(addr, out string address, out string protocol);

            CheckProtocol(protocol);

            if (protocol == Address.InProcProtocol)
            {
                if (UnregisterEndpoint(addr, this))
                    return;

                if (!m_inprocs.TryGetValue(addr, out Pipe pipe))
                    throw new EndpointNotFoundException("Endpoint was not found and cannot be disconnected");

                pipe.Terminate(true);
                m_inprocs.Remove(addr);
            }
            else
            {
                if (!m_endpoints.TryGetValue(addr, out Endpoint endpoint))
                    throw new EndpointNotFoundException("Endpoint was not found and cannot be disconnected");

                endpoint.Pipe?.Terminate(false);

                TermChild(endpoint.Own);
                m_endpoints.Remove(addr);
            }
        }

        /// <summary>
        /// Transmit the given Msg across the message-queueing system.
        /// </summary>
        /// <param name="msg">The <see cref="Msg"/> to send.</param>
        /// <param name="timeout">The timeout to wait before returning <c>false</c>. Pass <see cref="SendReceiveConstants.InfiniteTimeout"/> to disable timeout.</param>
        /// <param name="more">Whether this message will contain another frame after this one.</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        public bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            CheckContextTerminated();

            // Check whether message passed to the function is valid.
            if (!msg.IsInitialised)
                throw new FaultException("SocketBase.Send passed an uninitialised Msg.");

            // Process pending commands, if any.
            ProcessCommands(0, true);

            // Clear any user-visible flags that are set on the message.
            msg.ResetFlags(MsgFlags.More);

            // At this point we impose the flags on the message.
            if (more)
                msg.SetFlags(MsgFlags.More);

            // Try to send the message.
            bool isMessageSent = XSend(ref msg);

            if (isMessageSent)
                return true;

            // In case of non-blocking send we'll simply return false
            if (timeout == TimeSpan.Zero)
                return false;

            // Compute the time when the timeout should occur.
            // If the timeout is infinite, don't care.
            int timeoutMillis = (int)timeout.TotalMilliseconds;
            long end = timeoutMillis < 0 ? 0 : (Clock.NowMs() + timeoutMillis);

            // Oops, we couldn't send the message. Wait for the next
            // command, process it and try to send the message again.
            // If timeout is reached in the meantime, return EAGAIN.
            while (true)
            {
                ProcessCommands(timeoutMillis, false);

                isMessageSent = XSend(ref msg);

                if (isMessageSent)
                    break;

                if (timeoutMillis <= 0)
                    continue;

                timeoutMillis = (int)(end - Clock.NowMs());

                if (timeoutMillis <= 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Receive a frame into the given <paramref name="msg"/> and return <c>true</c> if successful, <c>false</c> if it timed out.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to read the received message into</param>
        /// <param name="timeout">this controls whether the call blocks, and for how long.</param>
        /// <returns><c>true</c> if successful, <c>false</c> if it timed out</returns>
        /// <remarks>
        /// For <paramref name="timeout"/>, there are three categories of value:
        /// <list type="bullet">
        ///   <item><see cref="TimeSpan.Zero"/> - return <c>false</c> immediately if no message is available</item>
        ///   <item><see cref="SendReceiveConstants.InfiniteTimeout"/> (or a negative value) - wait indefinitely, always returning <c>true</c></item>
        ///   <item>Any positive value - return <c>false</c> after the corresponding duration if no message has become available</item>
        /// </list>
        /// </remarks>
        /// <exception cref="FaultException">the Msg must already have been uninitialised</exception>
        /// <exception cref="TerminatingException">The socket must not already be stopped.</exception>
        public bool TryRecv(ref Msg msg, TimeSpan timeout)
        {
            CheckContextTerminated();

            // Check whether message passed to the function is valid.
            if (!msg.IsInitialised)
                throw new FaultException("SocketBase.Recv passed an uninitialised Msg.");

            // Get the message.
            bool isMessageAvailable = XRecv(ref msg);

            // Once every Config.InboundPollRate messages check for signals and process
            // incoming commands. This happens only if we are not polling altogether
            // because there are messages available all the time. If poll occurs,
            // ticks is set to zero and thus we avoid this code.
            //
            // Note that 'recv' uses different command throttling algorithm (the one
            // described above) from the one used by 'send'. This is because counting
            // ticks is more efficient than doing RDTSC all the time.
            if (++m_ticks == Config.InboundPollRate)
            {
                ProcessCommands(0, false);
                m_ticks = 0;
            }

            // If we have the message, return immediately.
            if (isMessageAvailable)
            {
                ExtractFlags(ref msg);
                return true;
            }

            // If the message cannot be fetched immediately, there are two scenarios.
            // For non-blocking recv, commands are processed in case there's an
            // activate_reader command already waiting in a command pipe.
            // If it's not, return false.
            if (timeout == TimeSpan.Zero)
            {
                ProcessCommands(0, false);
                m_ticks = 0;

                isMessageAvailable = XRecv(ref msg);

                if (!isMessageAvailable)
                    return false;

                ExtractFlags(ref msg);
                return true;
            }

            // Compute the time when the timeout should occur.
            // If the timeout is infinite (negative), don't care.
            int timeoutMillis = (int)timeout.TotalMilliseconds;
            long end = timeoutMillis < 0 ? 0L : Clock.NowMs() + timeoutMillis;

            // In blocking scenario, commands are processed over and over again until
            // we are able to fetch a message.
            bool block = m_ticks != 0;
            while (true)
            {
                ProcessCommands(block ? timeoutMillis : 0, false);

                isMessageAvailable = XRecv(ref msg);
                if (isMessageAvailable)
                {
                    m_ticks = 0;
                    break;
                }

                block = true;
                if (timeoutMillis > 0)
                {
                    timeoutMillis = (int)(end - Clock.NowMs());

                    if (timeoutMillis <= 0)
                        return false;
                }
            }

            ExtractFlags(ref msg);
            return true;
        }

        /// <summary>
        /// Close this socket. Mark it as disposed, and send ownership of it to the reaper thread to
        /// attend to the rest of it's shutdown process.
        /// </summary>
        public void Close()
        {
            // Mark the socket as disposed
            m_disposed = true;

            // Transfer the ownership of the socket from this application thread
            // to the reaper thread which will take care of the rest of shutdown
            // process.
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

        /// <summary>
        /// These functions are used by the polling mechanism to determine
        /// which events are to be reported from this socket.
        /// </summary>
        public bool HasOut()
        {
            return XHasOut();
        }

        /// <summary>
        /// Using this function reaper thread ask the socket to register with
        /// its poller.
        /// </summary>
        internal void StartReaping([NotNull] Poller poller)
        {
            // Plug the socket to the reaper thread.
            m_poller = poller;
            m_handle = m_mailbox.Handle;
            m_poller.AddHandle(m_handle, this);
            m_poller.SetPollIn(m_handle);

            // Initialise the termination and check whether it can be deallocated
            // immediately.
            Terminate();
            CheckDestroy();
        }

        /// <summary>
        /// Processes commands sent to this socket (if any).
        /// If <paramref name="timeout"/> is <c>-1</c>, the call blocks until at least one command was processed.
        /// If <paramref name="throttle"/> is <c>true</c>, commands are processed at most once in a predefined time period.
        /// </summary>
        /// <param name="timeout">how much time to allow to wait for a command, before returning (in milliseconds)</param>
        /// <param name="throttle">if true - throttle the rate of command-execution by doing only one per call</param>
        /// <exception cref="TerminatingException">The Ctx context must not already be terminating.</exception>
        private void ProcessCommands(int timeout, bool throttle)
        {
            bool found;
            Command command;
            if (timeout != 0)
            {
                // If we are asked to wait, simply ask mailbox to wait.
                found = m_mailbox.TryRecv(timeout, out command);
            }
            else
            {
                // If we are asked not to wait, check whether we haven't processed
                // commands recently, so that we can throttle the new commands.

                // Get the CPU's tick counter. If 0, the counter is not available.
                long tsc = Clock.Rdtsc();

                // Optimised version of command processing - it doesn't have to check
                // for incoming commands each time. It does so only if certain time
                // elapsed since last command processing. Command delay varies
                // depending on CPU speed: It's ~1ms on 3GHz CPU, ~2ms on 1.5GHz CPU
                // etc. The optimisation makes sense only on platforms where getting
                // a timestamp is a very cheap operation (tens of nanoseconds).
                if (tsc != 0 && throttle)
                {
                    // Check whether TSC haven't jumped backwards (in case of migration
                    // between CPU cores) and whether certain time have elapsed since
                    // last command processing. If it didn't do nothing.
                    if (tsc >= m_lastTsc && tsc - m_lastTsc <= Config.MaxCommandDelay)
                        return;
                    m_lastTsc = tsc;
                }

                // Check whether there are any commands pending for this thread.
                found = m_mailbox.TryRecv(0, out command);
            }

            // Process all the commands available at the moment.
            while (found)
            {
                command.Destination.ProcessCommand(command);
                found = m_mailbox.TryRecv(0, out command);
            }

            CheckContextTerminated();
        }

        /// <summary>
        /// Process a termination command on this socket, by stopping monitoring and marking this as terminated.
        /// </summary>
        protected override void ProcessStop()
        {
            // Here, someone have called zmq_term while the socket was still alive.
            // We'll remember the fact so that any blocking call is interrupted and any
            // further attempt to use the socket will raise TerminatingException.
            // The user is still responsible for calling zmq_close on the socket though!
            StopMonitor();
            m_isStopped = true;
        }

        /// <summary>
        /// Process a Bind command by attaching the given Pipe.
        /// </summary>
        /// <param name="pipe">the Pipe to attach</param>
        protected override void ProcessBind(Pipe pipe)
        {
            AttachPipe(pipe);
        }

        /// <summary>
        /// Process a termination request.
        /// </summary>
        /// <param name="linger">a time (in milliseconds) for this to linger before actually going away. -1 means infinite.</param>
        protected override void ProcessTerm(int linger)
        {
            // Unregister all inproc endpoints associated with this socket.
            // Doing this we make sure that no new pipes from other sockets (inproc)
            // will be initiated.
            UnregisterEndpoints(this);

            // Ask all attached pipes to terminate.
            for (int i = 0; i != m_pipes.Count; ++i)
                m_pipes[i].Terminate(false);
            RegisterTermAcks(m_pipes.Count);

            // Continue the termination process immediately.
            base.ProcessTerm(linger);
        }

        /// <summary>
        /// Mark this socket as having been destroyed. Delay actual destruction of the socket.
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
        /// <param name="option">a ZmqSocketOption specifying which option to set</param>
        /// <param name="optionValue">an Object that is the value to set the option to</param>
        protected virtual bool XSetSocketOption(ZmqSocketOption option, [CanBeNull] object optionValue)
        {
            return false;
        }

        /// <summary>
        /// This gets called when outgoing messages are ready to be sent out.
        /// On SocketBase, this does nothing and simply returns false.
        /// </summary>
        /// <returns>this method on SocketBase only returns false</returns>
        protected virtual bool XHasOut()
        {
            return false;
        }

        /// <summary>
        /// Transmit the given message. The <see cref="TrySend"/> method calls this to do the actual sending.
        /// This abstract method gets overridden by the different socket types
        /// to provide their concrete implementation of sending messages.
        /// </summary>
        /// <param name="msg">the message to transmit</param>
        /// <returns><c>true</c> if the message was sent successfully</returns>
        protected virtual bool XSend(ref Msg msg)
        {
            throw new NotSupportedException("Must Override");
        }

        /// <summary>
        /// This gets called when incoming messages are ready to be received.
        /// On SocketBase, this does nothing and simply returns false.
        /// </summary>
        /// <returns>this method on SocketBase only returns false</returns>
        protected virtual bool XHasIn()
        {
            return false;
        }

        /// <summary>
        /// Receive a message. The <c>Recv</c> method calls this lower-level method to do the actual receiving.
        /// This abstract method gets overridden by the different socket types
        /// to provide their concrete implementation of receiving messages.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to receive the message into</param>
        /// <returns><c>true</c> if the message was received successfully, <c>false</c> if there were no messages to receive</returns>
        protected virtual bool XRecv(ref Msg msg)
        {
            throw new NotSupportedException("Must Override");
        }

        /// <summary>
        /// Indicate the given pipe as being ready for reading by this socket.
        /// This abstract method gets overridden by the different sockets
        /// to provide their own concrete implementation.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for reading</param>
        protected virtual void XReadActivated([NotNull] Pipe pipe)
        {
            throw new NotSupportedException("Must Override");
        }

        /// <summary>
        /// Indicate the given pipe as being ready for writing to by this socket.
        /// This abstract method gets called by the WriteActivated method
        /// and gets overridden by the different sockets
        /// to provide their own concrete implementation.
        /// </summary>
        /// <param name="pipe">the <c>Pipe</c> that is now becoming available for writing</param>
        protected virtual void XWriteActivated([NotNull] Pipe pipe)
        {
            throw new NotSupportedException("Must Override");
        }

        protected virtual void XHiccuped([NotNull] Pipe pipe)
        {
            throw new NotSupportedException("Must override");
        }

        /// <summary>
        /// Handle input-ready events by receiving and processing any incoming commands.
        /// </summary>
        public virtual void InEvent()
        {
            // This function is invoked only once the socket is running in the context
            // of the reaper thread. Process any commands from other threads/sockets
            // that may be available at the moment. Ultimately, the socket will
            // be destroyed.

            try
            {
                ProcessCommands(0, false);
            }
            catch
            {
                // ignored
            }
            finally
            {
                CheckDestroy();
            }
        }

        /// <summary>
        /// Handle output-ready events.
        /// </summary>
        /// <exception cref="NotSupportedException">This is not supported on instances of the parent class SocketBase.</exception>
        public virtual void OutEvent()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// In subclasses of SocketBase this is overridden, to handle the expiration of a timer.
        /// </summary>
        /// <param name="id">an integer used to identify the timer</param>
        /// <exception cref="NotSupportedException">You must not call TimerEvent on an instance of class SocketBase.</exception>
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
            // If the object was already marked as destroyed, finish the deallocation.
            if (m_destroyed)
            {
                // Remove the socket from the reaper's poller.
                m_poller.RemoveHandle(m_handle);
                // Remove the socket from the context.
                DestroySocket(this);

                // Notify the reaper about the fact.
                SendReaped();

                // Deallocate.
                base.ProcessDestroy();
            }
        }

        /// <summary>
        /// Indicate that the given pipe is now ready for reading.
        /// Pipe calls this on it's sink in response to ProcessActivateRead.
        /// When called upon an instance of SocketBase, this simply calls XReadActivated.
        /// </summary>
        /// <param name="pipe">the pipe to indicate is ready for reading</param>
        public void ReadActivated(Pipe pipe)
        {
            XReadActivated(pipe);
        }

        /// <summary>
        /// When called upon an instance of SocketBase, this simply calls XWriteActivated.
        /// </summary>
        /// <param name="pipe">the pipe to indicate is ready for writing</param>
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

        /// <summary>
        /// This gets called by ProcessPipeTermAck or XTerminated to respond to the termination of the given pipe.
        /// </summary>
        /// <param name="pipe">the pipe that was terminated</param>
        public void Terminated(Pipe pipe)
        {
            // Notify the specific socket type about the pipe termination.
            XTerminated(pipe);

            // Remove pipe from inproc pipes
            var pipesToDelete = m_inprocs.Where(i => i.Value == pipe).Select(i => i.Key).ToArray();
            foreach (var addr in pipesToDelete)
            {
                m_inprocs.Remove(addr);
            }

            // Remove the pipe from the list of attached pipes and confirm its
            // termination if we are already shutting down.
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
            // Test whether IDENTITY flag is valid for this socket type.
            Debug.Assert(!msg.IsIdentity || m_options.RecvIdentity);

            // Remove MORE flag.
            m_rcvMore = msg.HasMore;
        }

        /// <summary>
        /// Register the given events to monitor on the given endpoint.
        /// </summary>
        /// <param name="addr">a string denoting the endpoint to monitor. If this is null - monitoring is stopped.</param>
        /// <param name="events">the SocketEvent to monitor for</param>
        /// <exception cref="NetMQException">Maximum number of sockets reached.</exception>
        /// <exception cref="ProtocolNotSupportedException">The protocol of <paramref name="addr"/> is not supported.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public void Monitor([CanBeNull] string addr, SocketEvents events)
        {
            CheckContextTerminated();

            // Support de-registering monitoring endpoints as well
            if (addr == null)
            {
                StopMonitor();
                return;
            }

            DecodeAddress(addr, out string address, out string protocol);

            CheckProtocol(protocol);

            // Event notification only supported over inproc://
            if (protocol != Address.InProcProtocol)
                throw new ProtocolNotSupportedException($"In SocketBase.Monitor({addr},), protocol must be inproc");

            // Register events to monitor
            m_monitorEvents = events;

            m_monitorSocket = Ctx.CreateSocket(ZmqSocketType.Pair);

            // Never block context termination on pending event messages
            const int linger = 0;

            try
            {
                m_monitorSocket.SetSocketOption(ZmqSocketOption.Linger, linger);
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

        #region Monitor events

        public void EventConnected([NotNull] string addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvents.Connected) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.Connected, addr, ch));
        }

        public void EventConnectDelayed([NotNull] string addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvents.ConnectDelayed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.ConnectDelayed, addr, errno));
        }

        public void EventConnectRetried([NotNull] string addr, int interval)
        {
            if ((m_monitorEvents & SocketEvents.ConnectRetried) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.ConnectRetried, addr, interval));
        }

        public void EventListening([NotNull] string addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvents.Listening) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.Listening, addr, ch));
        }

        public void EventBindFailed([NotNull] string addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvents.BindFailed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.BindFailed, addr, errno));
        }

        public void EventAccepted([NotNull] string addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvents.Accepted) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.Accepted, addr, ch));
        }

        public void EventAcceptFailed([NotNull] string addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvents.AcceptFailed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.AcceptFailed, addr, errno));
        }

        public void EventClosed([NotNull] string addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvents.Closed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.Closed, addr, ch));
        }

        public void EventCloseFailed([NotNull] string addr, ErrorCode errno)
        {
            if ((m_monitorEvents & SocketEvents.CloseFailed) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.CloseFailed, addr, errno));
        }

        public void EventDisconnected([NotNull] string addr, [NotNull] AsyncSocket ch)
        {
            if ((m_monitorEvents & SocketEvents.Disconnected) == 0)
                return;

            MonitorEvent(new MonitorEvent(SocketEvents.Disconnected, addr, ch));
        }

        private void MonitorEvent([NotNull] MonitorEvent monitorEvent)
        {
            if (m_monitorSocket == null)
                return;

            monitorEvent.Write(m_monitorSocket);
        }

        #endregion

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

        /// <summary>
        /// Override the ToString method in order to also show the socket-id.
        /// </summary>
        /// <returns>A string that denotes this object type followed by [ socket-id ]</returns>
        public override string ToString()
        {
            return base.ToString() + "[" + m_options.SocketId + "]";
        }

        /// <summary>
        /// Get the Socket (Handle) - which is actually the Handle of the contained mailbox.
        /// </summary>
        [NotNull]
        public Socket Handle => m_mailbox.Handle;

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
