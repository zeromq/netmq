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

namespace NetMQ.zmq
{
    public class TcpListener : Own, IPollEvents
    {

        //private static Logger LOG = LoggerFactory.getLogger(TcpListener.class);
        //  Address to listen on.
        private readonly TcpAddress m_address;

        //  Underlying socket.
        private Socket m_handle;

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
            //  Start polling for incoming connections.
            m_ioObject.SetHandler(this);
            m_ioObject.AddFd(m_handle);
            m_ioObject.SetPollin(m_handle);
        }


        protected override void ProcessTerm(int linger)
        {
            m_ioObject.SetHandler(this);
            m_ioObject.RmFd(m_handle);
            Close();
            base.ProcessTerm(linger);
        }


        public void InEvent()
        {
            Socket fd;

            try
            {
                fd = Accept();
                Utils.TuneTcpSocket(fd);
                Utils.TuneTcpKeepalives(fd, m_options.TcpKeepalive, m_options.TcpKeepaliveCnt, m_options.TcpKeepaliveIdle, m_options.TcpKeepaliveIntvl);
            }
            catch (NetMQException ex)
            {
                //  If connection was reset by the peer in the meantime, just ignore it.
                //  TODO: Handle specific errors like ENFILE/EMFILE etc.
                //ZError.exc (e);
                m_socket.EventAcceptFailed(m_endpoint, ex.ErrorCode);
                return;
            }


            //  Create the engine object for this connection.
            StreamEngine engine;
            try
            {
                engine = new StreamEngine(fd, m_options, m_endpoint);
            }
            catch (SocketException ex)
            {
                //LOG.error("Failed to initialize StreamEngine", e.getCause());

                ErrorCode errorCode = ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode);

                m_socket.EventAcceptFailed(m_endpoint, errorCode);
                throw NetMQException.Create(errorCode);
            }
            //  Choose I/O thread to run connecter in. Given that we are already
            //  running in an I/O thread, there must be at least one available.
            IOThread ioThread = ChooseIOThread(m_options.Affinity);

            //  Create and launch a session object. 
            SessionBase session = SessionBase.Create(ioThread, false, m_socket,
                                                     m_options, new Address(m_handle.LocalEndPoint));
            session.IncSeqnum();
            LaunchChild(session);
            SendAttach(session, engine, false);
            m_socket.EventAccepted(m_endpoint, fd);
        }


        //  Close the listening socket.
        private void Close()
        {
            if (m_handle == null)
                return;

            try
            {
                m_handle.Close();
                m_socket.EventClosed(m_endpoint, m_handle);
            }
            catch (SocketException ex)
            {
                m_socket.EventCloseFailed(m_endpoint, ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode));
            }
            catch (NetMQException ex)
            {
                m_socket.EventCloseFailed(m_endpoint, ex.ErrorCode);
            }
            m_handle = null;
        }

        public virtual String Address
        {
            get { return m_address.ToString(); }
        }

        //  Set address to listen on. return the used port
        public virtual void SetAddress(String addr)
        {
            m_address.Resolve(addr, m_options.IPv4Only);

            m_endpoint = m_address.ToString();
            try
            {
                m_handle =
                    new Socket(m_address.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                //handle.Blocking = false;

                m_handle.ExclusiveAddressUse = false;
                m_handle.Bind(m_address.Address);
                m_handle.Listen(m_options.Backlog);
            }
            catch (SocketException ex)
            {
                Close();
                throw NetMQException.Create(ex);
            }

            m_socket.EventListening(m_endpoint, m_handle);

            m_port = ((IPEndPoint)m_handle.LocalEndPoint).Port;
        }

        public virtual int Port
        {
            get { return m_port; }
        }

        //  Accept the new connection. Returns the file descriptor of the
        //  newly created connection. The function may return retired_fd
        //  if the connection was dropped while waiting in the listen backlog
        //  or was denied because of accept filters.
        private Socket Accept()
        {
            Socket sock = null;
            try
            {
                sock = m_handle.Accept();
            }
            catch (SocketException)
            {
                return null;
            }

            if (m_options.TcpAcceptFilters.Count > 0)
            {
                bool matched = false;
                foreach (TcpAddress.TcpAddressMask am in m_options.TcpAcceptFilters)
                {
                    if (am.MatchAddress(m_address.Address))
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    try
                    {
                        sock.Close();
                    }
                    catch (SocketException)
                    {
                    }
                    return null;
                }
            }
            return sock;
        }




        public void OutEvent()
        {
            throw new NotSupportedException();
        }


        public void TimerEvent(int id)
        {
            throw new NotSupportedException();
        }
    }
}
