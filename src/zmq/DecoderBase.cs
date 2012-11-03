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
using NetMQ;

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

abstract public class DecoderBase : IDecoder
{

    //  Where to store the read data.
    private ArraySegment<byte> read_pos;

    //  How much data to read before taking next step.
    protected int to_read;

    //  The buffer for data to decode.
    private int bufsize;
    private byte[] buf;

    public DecoderBase(int bufsize_)
    {
        to_read = 0;
        bufsize = bufsize_;
        buf = new byte[bufsize_];
        state = -1;
    }

    public abstract void set_msg_sink(IMsgSink msg_sink);
    public abstract bool stalled();


    //  Returns a buffer to be filled with binary data.
    public void get_buffer(ref ArraySegment<byte> data_)
    {
        //  If we are expected to read large message, we'll opt for zero-
        //  copy, i.e. we'll ask caller to fill the data directly to the
        //  message. Note that subsequent read(s) are non-blocking, thus
        //  each single read reads at most SO_RCVBUF bytes at once not
        //  depending on how large is the chunk returned from here.
        //  As a consequence, large messages being received won't block
        //  other engines running in the same I/O thread for excessive
        //  amounts of time.

        if (to_read >= bufsize)
        {
            data_ = new ArraySegment<byte>(read_pos.Array, read_pos.Offset, to_read);
            return;
        }

        data_ = new ArraySegment<byte>(buf, 0, bufsize);
    }


    //  Processes the data in the buffer previously allocated using
    //  get_buffer function. size_ argument specifies nemuber of bytes
    //  actually filled into the buffer. Function returns number of
    //  bytes actually processed.
    public int process_buffer(ArraySegment<byte> data)
    {
        //  Check if we had an error in previous attempt.
        if (state < 0)
        {
            return -1;
        }

        //  In case of zero-copy simply adjust the pointers, no copying
        //  is required. Also, run the state machine in case all the data
        //  were processed.
        if (data.Array == read_pos.Array && data.Offset == read_pos.Offset)
        {
            read_pos = new ArraySegment<byte>(read_pos.Array, read_pos.Offset + data.Count, read_pos.Count - data.Count);
            to_read -= data.Count;

            while (to_read == 0)
            {
                if (!IsNext())
                {
                    if (state < 0)
                    {
                        return -1;
                    }
                    return data.Count;
                }
            }
            return data.Count;
        }

        int pos = 0;
        while (true)
        {

            //  Try to get more space in the message to fill in.
            //  If none is available, return.
            while (to_read == 0)
            {
                if (!IsNext())
                {
                    if (state <0)
                    {
                        return -1;
                    }

                    return pos;
                }
            }

            //  If there are no more data in the buffer, return.
            if (pos == data.Count)
                return pos;

            //  Copy the data from buffer to the message.
            int to_copy = Math.Min(to_read, data.Count - pos);
            Buffer.BlockCopy(data.Array, data.Offset + pos, read_pos.Array, read_pos.Offset, to_copy);
            read_pos = new ArraySegment<byte>(read_pos.Array, read_pos.Offset + to_copy, read_pos.Count - to_copy);
            pos += to_copy;
            to_read -= to_copy;
        }
    }

    protected void next_step(ArraySegment<byte> read_pos_, int to_read_, int state_)
    {
        read_pos = read_pos_;
        to_read = to_read_;
        state = state_;
    }

    protected int state
    {
        get;
        set;
    }

    protected void decoding_error()
    {
        state = -1;
    }

    abstract protected bool IsNext();

}
