/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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
using NetMQ;
using System.Net.Sockets;
using System.Threading;
using System.Security.AccessControl;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

//  This is a cross-platform equivalent to signal_fd. However, as opposed
//  to signal_fd there can be at most one signal in the signaler at any
//  given moment. Attempt to send a signal before receiving the previous
//  one will result in undefined behaviour.

public class Signaler
{
    //  Underlying write & read file descriptor.
    private Socket w;
    private Socket r;

    public Signaler()
    {
        //  Create the socketpair for signaling.
        make_fdpair();

        //  Set both fds to non-blocking mode.
        w.Blocking = false;
        r.Blocking = false;
    }

    public void close()
    {
        try
        {
            w.Close();
        }
        catch (Exception)
        {
        }

        try
        {
            r.Close();
        }
        catch (Exception)
        {
        }
    }

    [DllImport("kernel32.dll")]
    static extern bool SetHandleInformation(IntPtr hObject, int dwMask, uint dwFlags);

    const int HANDLE_FLAG_INHERIT = 0x00000001;

    //  Creates a pair of filedescriptors that will be used
    //  to pass the signals.
    private void make_fdpair()
    {
        Mutex sync;

        try
        {
            sync = new Mutex(false,"Global\\zmq-signaler-port-sync");
        }
        catch (UnauthorizedAccessException)
        {
            sync = Mutex.OpenExisting("Global\\zmq-signaler-port-sync", MutexRights.Synchronize | MutexRights.Modify);
        }

        Debug.Assert(sync != null);

        sync.WaitOne();
       
        Socket listner = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified);
        listner.NoDelay = true;
        listner.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, Config.signaler_port);

        listner.Bind(endpoint);
        listner.Listen(1);

        w = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified);

        SetHandleInformation(w.Handle, HANDLE_FLAG_INHERIT, 0);
        w.NoDelay = true;

        w.Connect(endpoint);

        r = listner.Accept();

        SetHandleInformation(r.Handle, HANDLE_FLAG_INHERIT, 0);

        listner.Close();

        sync.ReleaseMutex();
    }

    public System.Net.Sockets.Socket get_fd()
    {
        return r;
    }

    public void send()
    {
        byte[] dummy = new byte[1]{0};
        int nbytes = w.Send(dummy);

        Debug.Assert(nbytes == 1);        
    }

    public bool wait_event(int timeout_)
    {
        return r.Poll(timeout_ % 1000 * 1000, SelectMode.SelectRead);           
    }

    public void recv()
    {
        byte[] dummy = new byte[1];

        int nbytes = r.Receive(dummy);

        Debug.Assert(nbytes == 1);
        Debug.Assert(dummy[0] == 0);
    }




}
