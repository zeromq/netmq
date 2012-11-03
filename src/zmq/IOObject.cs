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
using System.Diagnostics;

//  Simple base class for objects that live in I/O threads.
//  It makes communication with the poller object easier and
//  makes defining unneeded event handlers unnecessary.

public class IOObject : IPollEvents {

    private Poller poller;
    private IPollEvents handler;
    
    public IOObject(IOThread io_thread_) {
        if (io_thread_ != null) {
            plug(io_thread_);
        }
    }

    //  When migrating an object from one I/O thread to another, first
    //  unplug it, then migrate it, then plug it to the new thread.
    
    public void plug(IOThread io_thread_) {
        
        
        Debug.Assert(io_thread_ != null);
        Debug.Assert(poller == null);

        //  Retrieve the poller from the thread we are running in.
        poller = io_thread_.get_poller ();    
    }
    
    public void unplug() {
        Debug.Assert(poller != null);

        //  Forget about old poller in preparation to be migrated
        //  to a different I/O thread.
        poller = null;
        handler = null;
    }

    public void add_fd (System.Net.Sockets.Socket fd_)
    {
        poller.add_fd (fd_, this);
    }
    
    public void rm_fd(System.Net.Sockets.Socket handle) {
        poller.rm_fd(handle);
    }
    
    public void set_pollin (System.Net.Sockets.Socket handle_)
    {
        poller.set_pollin (handle_);
    }

    public void set_pollout (System.Net.Sockets.Socket handle_)
    {
        poller.set_pollout (handle_);
    }    
    
    public void reset_pollin(System.Net.Sockets.Socket handle) {
        poller.reset_pollin (handle);
    }


    public void reset_pollout(System.Net.Sockets.Socket handle) {
        poller.reset_pollout (handle);
    }

    public void in_event() {
        handler.in_event();
    }

    public void out_event() {
        handler.out_event();
    }
    
    
    public void timer_event(int id_) {
        handler.timer_event(id_);
    }
    
    public void add_timer (long timeout_, int id_)
    {
        poller.add_timer (timeout_, this, id_);
    }

    public void set_handler(IPollEvents handler) {
        this.handler = handler;
    }




    public void cancel_timer(int id_) {
        poller.cancel_timer(this, id_);
    }



}
