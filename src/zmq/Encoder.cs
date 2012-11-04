/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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
using NetMQ;
using zmq;


public class Encoder : EncoderBase {

    private const int SizeReady = 0;
    private const int MessageReady = 1;
    

    private Msg in_progress;
    private ByteArraySegment tmpbuf;

    private IMsgSource msg_source;
    
    public Encoder (int bufsize_) : base(bufsize_)
    {        
        tmpbuf = new byte[10];


        //  Write 0 bytes to the batch and go to message_ready state.
        next_step(tmpbuf, 0, MessageReady, true);
    }

    public override void set_msg_source (IMsgSource msg_source_)
    {
        msg_source = msg_source_;
    }
    
    protected override  bool isNext() {
        switch(state) {
        case SizeReady:
            return IsSizeReady();
        case MessageReady:
            return IsMessageReady ();
        default:
            return false;
        }
    }

    
    private bool IsSizeReady ()
    {
        //  Write message body into the buffer.
        next_step (in_progress.get_data(),in_progress.size ,MessageReady, !in_progress.has_more());
        return true;
    }

    
    private bool IsMessageReady ()
    {
        //  Destroy content of the old message.
        // in_progress.close ();

        //  Read new message. If there is none, return false.
        //  Note that new state is set only if write is successful. That way
        //  unsuccessful write will cause retry on the next state machine
        //  invocation.
        
        if (msg_source == null)
            return false;
        
        in_progress = msg_source.pull_msg ();
        if (in_progress == null) {
            return false;
        }

        //  Get the message size.
        int size = in_progress.size;

        //  Account for the 'flags' byte.
        size++;

        //  For messages less than 255 bytes long, write one byte of message size.
        //  For longer messages write 0xff escape character followed by 8-byte
        //  message size. In both cases 'flags' field follows.
        
        if (size < 255) {
            tmpbuf[0] = (byte)size;
            tmpbuf[1] = (byte) (in_progress.flags & MsgFlags.More);
            next_step(tmpbuf, 2, SizeReady, false);
        }
        else {
            tmpbuf[0] = 0xff;
            tmpbuf.PutLong(size, 1);
						tmpbuf[9] = (byte)(in_progress.flags & MsgFlags.More);

            next_step(tmpbuf, 10, SizeReady, false);
        }
        
        return true;
    }

}
