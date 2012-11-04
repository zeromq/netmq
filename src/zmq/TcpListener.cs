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
using System.Net.Sockets;
using System.Diagnostics;
using System.Net;
using System.Security;
using System.Runtime.InteropServices;


public class TcpListener : Own, IPollEvents
{

    //private static Logger LOG = LoggerFactory.getLogger(TcpListener.class);
    //  Address to listen on.
    private TcpAddress address;

    //  Underlying socket.
    private System.Net.Sockets.Socket handle;

    //  Socket the listerner belongs to.
    private SocketBase socket;

    // String representation of endpoint to bind to
    private String endpoint;

    private IOObject io_object;

    public TcpListener(IOThread io_thread_, SocketBase socket_, Options options_) :
        base(io_thread_, options_)
    {

        io_object = new IOObject(io_thread_);
        address = new TcpAddress();
        handle = null;
        socket = socket_;
    }

    public override void destroy()
    {
        Debug.Assert(handle == null);
    }


    protected override void process_plug()
    {
        //  Start polling for incoming connections.
        io_object.set_handler(this);
        io_object.add_fd(handle);
        io_object.set_pollin(handle);
    }


    protected override void process_term(int linger_)
    {
        io_object.set_handler(this);
        io_object.rm_fd(handle);
        close();
        base.process_term(linger_);
    }


    public void in_event()    
    {
        Socket fd = null;

        try
        {
            fd = accept();
            Utils.tune_tcp_socket(fd);
            Utils.tune_tcp_keepalives(fd, options.TcpKeepalive, options.TcpKeepaliveCnt, options.TcpKeepaliveIdle, options.TcpKeepaliveIntvl);
        }
        catch (Exception)
        {
            //  If connection was reset by the peer in the meantime, just ignore it.
            //  TODO: Handle specific errors like ENFILE/EMFILE etc.
            //ZError.exc (e);
            socket.event_accept_failed(endpoint, ZError.errno);
            return;
        }


        //  Create the engine object for this connection.
        StreamEngine engine = null;
        try
        {
            engine = new StreamEngine(fd, options, endpoint);
        }
        catch (SocketException)
        {
            //LOG.error("Failed to initialize StreamEngine", e.getCause());
            ZError.errno = (ZError.EINVAL);
            socket.event_accept_failed(endpoint, ZError.errno);
            return;
        }
        //  Choose I/O thread to run connecter in. Given that we are already
        //  running in an I/O thread, there must be at least one available.
        IOThread io_thread = choose_io_thread(options.Affinity);

        //  Create and launch a session object. 
        SessionBase session = SessionBase.create(io_thread, false, socket,
            options, new Address(handle.LocalEndPoint));
        session.inc_seqnum();
        launch_child(session);
        send_attach(session, engine, false);
        socket.event_accepted(endpoint, fd);
    }


    //  Close the listening socket.
    private void close()
    {
        if (handle == null)
            return;

        try
        {
            handle.Close();
            socket.event_closed(endpoint, handle);
        }
        catch (Exception)
        {
            //ZError.exc (e);
            socket.event_close_failed(endpoint, ZError.errno);
        }
        handle = null;
    }

    public virtual String get_address()
    {
        return address.ToString();
    }

    [DllImport("kernel32.dll")]
    static extern bool SetHandleInformation(IntPtr hObject, int dwMask, uint dwFlags);

    const int HANDLE_FLAG_INHERIT = 0x00000001;


    //  Set address to listen on.
    public virtual bool set_address(String addr_)
    {
        address.resolve(addr_, options.IPv4Only > 0 ? true : false);

        endpoint = address.ToString();
        try
        {
            handle =
                new Socket(address.address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            SetHandleInformation(handle.Handle, HANDLE_FLAG_INHERIT, 0); 
            
            //handle.Blocking = false;
            
            handle.ExclusiveAddressUse = false;
            handle.Bind((IPEndPoint)address.address);
            handle.Listen(options.Backlog);           
        }
        catch (SecurityException)
        {
            ZError.errno = (ZError.EACCESS);
            close();
            return false;
        }
        catch (ArgumentException)
        {
            ZError.errno = (ZError.ENOTSUP);
            close();
            return false;
        }
        catch (SocketException)
        {
            //ZError.exc(e);
            close();
            return false;
        }

        socket.event_listening(endpoint, handle);
        return true;
    }

    //  Accept the new connection. Returns the file descriptor of the
    //  newly created connection. The function may return retired_fd
    //  if the connection was dropped while waiting in the listen backlog
    //  or was denied because of accept filters.
    private Socket accept()
    {
        Socket sock = null;
        try
        {
            sock = handle.Accept();
        }
        catch (SocketException)
        {
            return null;
        }

        SetHandleInformation(sock.Handle, HANDLE_FLAG_INHERIT, 0);

        if (options.TcpAcceptFilters.Count > 0)
        {
            bool matched = false;
            foreach (TcpAddress.TcpAddressMask am in options.TcpAcceptFilters)
            {
                if (am.match_address(address.address))
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

    
    

    public void out_event()
    {
        throw new NotSupportedException();
    }

    
    public void timer_event(int id_)
    {
        throw new NotSupportedException();
    }
}
