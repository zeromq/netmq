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

//  Helper base class for decoders that know the amount of data to read
//  in advance at any moment. Knowing the amount in advance is a property
//  of the protocol used. 0MQ framing protocol is based size-prefixed
//  paradigm, which qualifies it to be parsed by this class.
//  On the other hand, XML-based transports (like XMPP or SOAP) don't allow
//  for knowing the size of data to read in advance and should use different
//  decoding algorithms.
//
//  This class , the state machine that parses the incoming buffer.
//  Derived class should implement individual state machine actions.

public class Decoder : DecoderBase {
    
    private const int one_byte_size_ready = 0;
    private const int eight_byte_size_ready = 1;
    private const int flags_ready = 2;
    private const int message_ready = 3;
    
    private byte[] tmpbuf;
    private Msg in_progress;
    private long maxmsgsize;
    private IMsgSink msg_sink;

    public Decoder (int bufsize_, long maxmsgsize_) : base(bufsize_)
    {
        maxmsgsize = maxmsgsize_;
        tmpbuf = new byte[8];
           
        //  At the beginning, read one byte and go to one_byte_size_ready state.
        next_step (new ArraySegment<byte>(tmpbuf,0,1), 1, one_byte_size_ready);
    }
    
    //  Set the receiver of decoded messages.    
    public override void set_msg_sink(IMsgSink msg_sink_) 
    {
        msg_sink = msg_sink_;
    }

    protected override bool IsNext() {
        switch(state) {
        case one_byte_size_ready:
            return is_one_byte_size_ready ();
        case eight_byte_size_ready:
            return is_eight_byte_size_ready ();
        case flags_ready:
            return is_flags_ready ();
        case message_ready:
            return is_message_ready ();
        default:
            return false;
        }
    }

    private bool is_one_byte_size_ready() {
        //  First byte of size is read. If it is 0xff read 8-byte size.
        //  Otherwise allocate the buffer for message data and read the
        //  message data into it.
        byte first = tmpbuf[0];
        if (first == 0xff) {
            next_step (new ArraySegment<byte>(tmpbuf, 0,8), 8, eight_byte_size_ready);
        } else {

            //  There has to be at least one byte (the flags) in the message).
            if (first == 0) {
                decoding_error ();
                return false;
            }                       

            //  in_progress is initialised at this point so in theory we should
            //  close it before calling zmq_msg_init_size, however, it's a 0-byte
            //  message and thus we can treat it as uninitialised...
            if (maxmsgsize >= 0 && (long) (first -1) > maxmsgsize) {
                decoding_error ();
                return false;

            }
            else {
                in_progress = new Msg(first-1);
            }

            next_step (new ArraySegment<byte>(tmpbuf, 0, 1),1, flags_ready);
        }
        return true;
    }
    
    private bool is_eight_byte_size_ready() {
        //  8-byte payload length is read. Allocate the buffer
        //  for message body and read the message data into it.
        long payload_length = BitConverter.ToInt64(tmpbuf, 0);

        //  There has to be at least one byte (the flags) in the message).
        if (payload_length == 0) {
            decoding_error ();
            return false;
        }

        //  Message size must not exceed the maximum allowed size.
        if (maxmsgsize >= 0 && payload_length - 1 > maxmsgsize) {
            decoding_error ();
            return false;
        }

        //  Message size must fit within range of size_t data type.
        if (payload_length - 1 > int.MaxValue) {
            decoding_error ();
            return false;
        }

        int msg_size =  (int)(payload_length - 1);
        //  in_progress is initialized at this point so in theory we should
        //  close it before calling init_size, however, it's a 0-byte
        //  message and thus we can treat it as uninitialized...
        in_progress = new Msg(msg_size);
        
        next_step (new ArraySegment<byte>(tmpbuf, 0, 1), 1, flags_ready);
        
        return true;

    }
    
    private bool is_flags_ready() {

        //  Store the flags from the wire into the message structure.
        
        int first = tmpbuf[0];
        
        in_progress.SetFlags (first & Msg.more);

        next_step (new ArraySegment<byte>(in_progress.get_data()),in_progress.size,message_ready);

        return true;
    }
    
    private bool is_message_ready() {
        //  Message is completely read. Push it further and start reading
        //  new message. (in_progress is a 0-byte message after this point.)
        
        if (msg_sink == null)
            return false;
        
        bool rc = msg_sink.push_msg (in_progress);
        if (!rc) {
            return false;
        }
        
        next_step (new ArraySegment<byte>(tmpbuf, 0, 1), 1, one_byte_size_ready);
        
        return true;
    }


    //  Returns true if there is a decoded message
    //  waiting to be delivered to the session.
    
    public override bool  stalled ()
    {
        return state == message_ready;
    }


}
