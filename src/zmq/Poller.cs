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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Linq;

public class Poller : PollerBase
{

    private class PollSet
    {
        public IPollEvents handler;
        public bool cancelled;

        public PollSet(IPollEvents handler)
        {
            this.handler = handler;
            cancelled = false;
        }
    }
    //  This table stores data for registered descriptors.
    private Dictionary<Socket, PollSet> fd_table;

    //  If true, there's at least one retired event source.
    private bool retired;

    //  If true, thread is in the process of shutting down.
    volatile private bool stopping;
    volatile private bool stopped;

    private Thread worker;
    private String name;

    HashSet<Socket> checkRead = new HashSet<Socket>();
    HashSet<Socket> checkWrite = new HashSet<Socket>();
    HashSet<Socket> checkError = new HashSet<Socket>();
    

    public Poller()
        : this("poller")
    {

    }

    public Poller(String name_)
    {

        name = name_;
        retired = false;
        stopping = false;
        stopped = false;

        fd_table = new Dictionary<Socket, PollSet>();
    }

    public void destroy()
    {
        if (!stopped)
        {
            try
            {
                worker.Join();
            }
            catch (Exception)
            {
            }
        }
    }
    public void add_fd(Socket fd_, IPollEvents events_)
    {
        fd_table.Add(fd_, new PollSet(events_));

        checkError.Add(fd_);

        adjust_load(1);
    }


    public void rm_fd(Socket handle)
    {
        fd_table[handle].cancelled = true;
        retired = true;

        checkError.Remove(handle);
        checkRead.Remove(handle);
        checkWrite.Remove(handle);

        //  Decrease the load metric of the thread.
        adjust_load(-1);
    }


    public void set_pollin(System.Net.Sockets.Socket handle_)
    {
        if (!checkRead.Contains(handle_))
            checkRead.Add(handle_);
    }


    public void reset_pollin(System.Net.Sockets.Socket handle_)
    {        
        checkRead.Remove(handle_);
    }

    public void set_pollout(System.Net.Sockets.Socket handle_)
    {
        if (!checkWrite.Contains(handle_))        
            checkWrite.Add(handle_);
    }

    public void reset_pollout(System.Net.Sockets.Socket handle_)
    {
        checkWrite.Remove(handle_);
    }           
    
    public void start()
    {
        worker = new Thread(loop);
        worker.Name = name;
        worker.Start();
    }

    public void stop()
    {
        stopping = true;
    }


    public void loop()
    {
        ArrayList readList = new ArrayList();
        ArrayList writeList = new ArrayList();
        ArrayList errorList = new ArrayList();

        while (!stopping)
        {
            //  Execute any due timers.
            int timeout = execute_timers();

            readList.AddRange((ICollection)checkRead.ToArray());
            writeList.AddRange((ICollection)checkWrite.ToArray());
            errorList.AddRange((ICollection)checkError.ToArray());

            try
            {
                Socket.Select(readList, writeList, errorList, timeout != 0 ? timeout : -1);
            }
            catch (SocketException)
            {
                continue;
            }

            foreach (Socket socket in errorList)
            {
                PollSet item; 

                if (fd_table.TryGetValue(socket, out item) && !item.cancelled)
                {
                    item.handler.in_event();
                }
            }
            errorList.Clear();
            

            foreach (Socket socket in writeList)
            {
                PollSet item;

                if (fd_table.TryGetValue(socket, out item) && !item.cancelled)
                {
                    item.handler.out_event();
                }
            }
            writeList.Clear();
            

            foreach (Socket socket in readList)
            {
                PollSet item;

                if (fd_table.TryGetValue(socket, out item) && !item.cancelled)
                {
                    item.handler.in_event();
                }
            }
            readList.Clear();

            
            if (retired)
            {
                foreach (var item in fd_table.Where(k => k.Value.cancelled).ToList())
                {
                    fd_table.Remove(item.Key);
                }

                retired = false;
            }                     
        }
    }


}
