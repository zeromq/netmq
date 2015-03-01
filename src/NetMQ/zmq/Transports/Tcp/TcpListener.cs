/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2010 iMatix Corporation
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
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.zmq.Transports.Tcp
{
    internal class TcpListener : Own, IProactorEvents
    {
        private const SocketOptionName IPv6Only = (SocketOptionName)27;

        [NotNull]
        private readonly IOObject m_ioObject;

        /// <summary>
        /// Address to listen on.
        /// </summary>
        [NotNull]
        private readonly TcpAddress m_address;

        /// <summary>
        /// Underlying socket.
        /// </summary>
        [CanBeNull]
        private AsyncSocket m_handle;

        /// <summary>
        /// socket being accepted
        /// </summary>
        private AsyncSocket m_acceptedSocket;

        /// <summary>
        /// Socket the listener belongs to.
        /// </summary>
        [NotNull]
        private readonly SocketBase m_socket;

        /// <summary>
        /// String representation of endpoint to bind to
        /// </summary>
        private String m_endpoint;

        /// <summary>
        /// The port that was bound on
        /// </summary>
        private int m_port;

        public TcpListener([NotNull] IOThread ioThread, [NotNull] SocketBase socket, [NotNull] Options options)
            : base(ioThread, options)
        {
            m_ioObject = new IOObject(ioThread);
            m_address = new TcpAddress();
            m_handle = null;
            m_socket = socket;
        }

        public override void Destroy()
        {
            Debug.Assert(m_handle == null);
        }

        protected override void ProcessPlug()
        {
            m_ioObject.SetHandler(this);
            m_ioObject.AddSocket(m_handle);

            Accept();
        }

        protected override void ProcessTerm(int linger)
        {
            m_ioObject.SetHandler(this);
            m_ioObject.RemoveSocket(m_handle);
            Close();
            base.ProcessTerm(linger);
        }

        /// <summary>
        /// Set address to listen on. return the used port
        /// </summary>
        /// <param name="addr"></param>
        public virtual void SetAddress([NotNull] string addr)
        {
            m_address.Resolve(addr, m_options.IPv4Only);

            try
            {
                m_handle = AsyncSocket.Create(m_address.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
                Debug.Assert(m_handle != null);

                if (!m_options.IPv4Only && m_address.Address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    try
                    {
                        // This is not supported on old windows operation system and might throw exception
                        m_handle.SetSocketOption(SocketOptionLevel.IPv6, IPv6Only, 0);
                    }
                    catch
                    {
                    }
                }

                m_handle.ExclusiveAddressUse = false;
                m_handle.Bind(m_address.Address);
                m_handle.Listen(m_options.Backlog);

                // Copy the port number after binding in case we requested a system-allocated port number (TCP port zero)
                m_address.Address.Port = m_handle.LocalEndPoint.Port;
                m_endpoint = m_address.ToString();

                m_socket.EventListening(m_endpoint, m_handle);

                m_port = m_handle.LocalEndPoint.Port;
            }
            catch (SocketException ex)
            {
                Close();
                throw NetMQException.Create(ex);
            }
        }

        private void Accept()
        {
            m_acceptedSocket = AsyncSocket.Create(m_address.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // start accepting socket async
            m_handle.Accept(m_acceptedSocket);
        }

        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success)
            {
                if (socketError == SocketError.ConnectionReset || socketError == SocketError.NoBufferSpaceAvailable ||
                    socketError == SocketError.TooManyOpenSockets)
                {
                    m_socket.EventAcceptFailed(m_endpoint, ErrorHelper.SocketErrorToErrorCode(socketError));

                    Accept();
                }
                else
                {
                    m_acceptedSocket.Dispose();

                    NetMQException exception = NetMQException.Create(socketError);

                    m_socket.EventAcceptFailed(m_endpoint, exception.ErrorCode);
                    throw exception;
                }
            }
            else
            {
                // TODO: check TcpFilters

                m_acceptedSocket.NoDelay = true;

                if (m_options.TcpKeepalive != -1)
                {
                    m_acceptedSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, m_options.TcpKeepalive);

                    if (m_options.TcpKeepaliveIdle != -1 && m_options.TcpKeepaliveIntvl != -1)
                    {
                        var bytes = new ByteArraySegment(new byte[12]);

                        Endianness endian = BitConverter.IsLittleEndian ? Endianness.Little : Endianness.Big;

                        bytes.PutInteger(endian, m_options.TcpKeepalive, 0);
                        bytes.PutInteger(endian, m_options.TcpKeepaliveIdle, 4);
                        bytes.PutInteger(endian, m_options.TcpKeepaliveIntvl, 8);

                        m_acceptedSocket.IOControl(IOControlCode.KeepAliveValues, (byte[])bytes, null);
                    }
                }

                //  Create the engine object for this connection.
                var engine = new StreamEngine(m_acceptedSocket, m_options, m_endpoint);

                //  Choose I/O thread to run connecter in. Given that we are already
                //  running in an I/O thread, there must be at least one available.
                IOThread ioThread = ChooseIOThread(m_options.Affinity);

                //  Create and launch a session object. 
                // TODO: send null in address parameter, is unneeded in this case
                SessionBase session = SessionBase.Create(ioThread, false, m_socket, m_options, new Address(m_handle.LocalEndPoint));
                session.IncSeqnum();
                LaunchChild(session);

                SendAttach(session, engine, false);

                m_socket.EventAccepted(m_endpoint, m_acceptedSocket);

                Accept();
            }
        }

        /// <summary>
        /// Close the listening socket.
        /// </summary>
        private void Close()
        {
            if (m_handle == null)
                return;

            try
            {
                m_handle.Dispose();
                m_socket.EventClosed(m_endpoint, m_handle);
            }
            catch (SocketException ex)
            {
                m_socket.EventCloseFailed(m_endpoint, ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode));
            }

            if (m_acceptedSocket != null)
            {
                try
                {
                    m_acceptedSocket.Dispose();
                }
                catch (SocketException)
                {
                }
            }

            m_acceptedSocket = null;
            m_handle = null;
        }

        /// <summary>
        /// Get the bound address for use with wildcards
        /// </summary>
        [NotNull]
        public virtual String Address
        {
            get { return m_address.ToString(); }
        }

        public virtual int Port
        {
            get { return m_port; }
        }

        void IProactorEvents.OutCompleted(SocketError socketError, int bytesTransferred)
        {
            throw new NotImplementedException();
        }

        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}
