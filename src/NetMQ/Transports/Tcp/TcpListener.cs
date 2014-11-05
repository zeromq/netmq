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
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security;
using System.Runtime.InteropServices;
using AsyncIO;
using NetMQ.Core;

namespace NetMQ.Transports.Tcp
{
    class TcpListener : Own, IProcatorEvents
    {
        private const SocketOptionName IPv6Only = (SocketOptionName) 27;

        //  Address to listen on.
        private readonly TcpAddress m_address;

        //  Underlying socket.
        private AsyncSocket m_handle;

        // socket being accepted
        private AsyncSocket m_acceptedSocket;

        //  Socket the listerner belongs to.
        private readonly SocketBase m_socket;

        // String representation of endpoint to bind to
        private String m_endpoint;

        private readonly IOObject m_ioObject;

        // The port that was bound on
        private int m_port;

        public TcpListener(IOThread ioThread, SocketBase socket, Options options) :
            base(ioThread, options)
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
        }

        protected override void ProcessTerm(int linger)
        {
            m_ioObject.SetHandler(this);
            m_ioObject.RemoveSocket(m_handle);
            Close();
            base.ProcessTerm(linger);
        }

        //  Set address to listen on. return the used port
        public virtual void SetAddress(String addr)
        {
            m_address.Resolve(addr, m_options.IPv4Only);

            m_endpoint = m_address.ToString();
            try
            {
                m_handle = AsyncSocket.Create(m_address.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

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

                m_socket.EventListening(m_endpoint, m_handle);

                m_port = m_handle.LocalEndPoint.Port;
                
                Accept();
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

                    ErrorCode errorCode = ErrorHelper.SocketErrorToErrorCode(socketError);

                    m_socket.EventAcceptFailed(m_endpoint, errorCode);
                    throw NetMQException.Create(errorCode);
                }
            }
            else
            {
                m_socket.EventAccepted(m_endpoint, m_acceptedSocket);

                // TODO: check TcpFilters

                m_acceptedSocket.NoDelay = true;                
                
                //Utils.TuneTcpKeepalives(m_acceptedSocket, m_options.TcpKeepalive, m_options.TcpKeepaliveCnt, m_options.TcpKeepaliveIdle, m_options.TcpKeepaliveIntvl);

                //  Create the engine object for this connection.
                StreamEngine engine = new StreamEngine(m_acceptedSocket, m_options, m_endpoint);

                //  Choose I/O thread to run connecter in. Given that we are already
                //  running in an I/O thread, there must be at least one available.
                IOThread ioThread = ChooseIOThread(m_options.Affinity);

                //  Create and launch a session object. 
                // TODO: send null in address parameter, is unneed in this case
                SessionBase session = SessionBase.Create(ioThread, false, m_socket, m_options, new Address(m_handle.LocalEndPoint));
                session.IncSeqnum();
                LaunchChild(session);

                SendAttach(session, engine, false);                

                Accept();
            }
        }

        //  Close the listening socket.
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

            try
            {
                m_acceptedSocket.Dispose();
            }
            catch (SocketException)
            {                                
            }

            m_acceptedSocket = null;
            m_handle = null;
        }

        public virtual String Address
        {
            get { return m_address.ToString(); }
        }        

        public virtual int Port
        {
            get { return m_port; }
        }

        void IProcatorEvents.OutCompleted(SocketError socketError, int bytesTransferred)
        {
            throw new NotImplementedException();
        }


        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}
