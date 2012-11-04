/*
    Copyright (c) 2007-2011 iMatix Corporation
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
using zmq;

public class Proxy {
    
    public static bool proxy (SocketBase frontend_,
            SocketBase backend_, SocketBase capture_)
    {

        //  The algorithm below assumes ratio of requests and replies processed
        //  under full load to be 1:1.

        //  TODO: The current implementation drops messages when
        //  any of the pipes becomes full.

        bool success = true;
        int rc;
        long more;
        Msg msg;
        PollItem[] items = new PollItem[2];
        
        items[0] = new PollItem (frontend_, ZMQ.ZmqPollin );
        items[1] = new PollItem (backend_, ZMQ.ZmqPollin );
        
        Selector selector;
        try {
            selector = Selector.open();
        } catch (IOException e) {
            throw new ZError.IOException(e);
        }
        
        try {
            while (!Thread.currentThread().isInterrupted()) {
                //  Wait while there are either requests or replies to process.
                rc = ZMQ.zmq_poll (selector, items, -1);
                if (rc < 0)
                    return false;
    
                //  Process a request.
                if (items [0].isReadable()) {
                    while (true) {
                        msg = frontend_.Recv (0);
                        if (msg == null) {
                            return false;
                        }
    
                        more = frontend_.getsockopt (ZMQ.ZMQ_RCVMORE);
                        
                        if (more < 0)
                            return false;
                        
                        //  Copy message to capture socket if any
                        if (capture_ != null) {
                            Msg ctrl = new Msg (msg);
                            success = capture_.Send (ctrl, more > 0 ? ZMQ.ZMQ_SNDMORE: 0);
                            if (!success)
                                return false;
                        }

    
                        success = backend_.Send (msg, more > 0 ? ZMQ.ZMQ_SNDMORE: 0);
                        if (!success)
                            return false;
                        if (more == 0)
                            break;
                    }
                }
                //  Process a reply.
                if (items [1].isReadable()) {
                    while (true) {
                        msg = backend_.Recv (0);
                        if (msg == null) {
                            return false;
                        }
    
                        more = backend_.getsockopt (ZMQ.ZMQ_RCVMORE);
                        
                        if (more < 0)
                            return false;
                        
                        //  Copy message to capture socket if any
                        if (capture_ != null) {
                            Msg ctrl = new Msg (msg);
                            success = capture_.Send (ctrl, more > 0 ? ZMQ.ZMQ_SNDMORE: 0);
                            if (!success)
                                return false;
                        }
    
                        success = frontend_.Send (msg, more > 0 ? ZMQ.ZMQ_SNDMORE: 0);
                        if (!success)
                            return false;
                        if (more == 0)
                            break;
                    }
                }
    
            }
        } finally {
            try {
                selector.close();
            } catch (Exception e) {
            }
        }
        
        return true;
    }
}
