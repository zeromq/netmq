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
using System.Runtime.InteropServices;
using zmq;

abstract public class EncoderBase : IEncoder
{

    //  Where to get the data to write from.
    //    private byte[] write_array;
    private ByteArraySegment write_pos;

    //  If true, first byte of the message is being written.
    //    @SuppressWarnings("unused")
    private bool beginning;


    //  How much data to write before next step should be executed.
    private int to_write;

    //  The buffer for encoded data.
    private byte[] buf;

    private int buffersize;


    private bool error;

    protected EncoderBase(int bufsize_)
    {
        buffersize = bufsize_;
        buf = new byte[bufsize_];
        error = false;
    }

    public abstract void set_msg_source(IMsgSource msg_source_);

    //  The function returns a batch of binary data. The data
    //  are filled to a supplied buffer. If no buffer is supplied (data_
    //  points to NULL) decoder object will provide buffer of its own.

    public void get_data(ref ByteArraySegment data, ref int size)
    {
        ByteArraySegment buffer = data == null ? new ByteArraySegment(buf) : data;
        int bufferArraySize = data == null ? buffersize : size;

        int pos = 0;

        while (pos < bufferArraySize)
        {
            //  If there are no more data to return, run the state machine.
            //  If there are still no data, return what we already have
            //  in the buffer.
            if (to_write == 0)
            {
                //  If we are to encode the beginning of a new message,
                //  adjust the message offset.

                //if (beginning)
                //{
                //    if (offest != null && offest.Value == -1)
                //    {
                //        offest = pos;
                //    }
                //}

                if (!isNext())
                    break;
            }

            //  If there are no data in the buffer yet and we are able to
            //  fill whole buffer in a single go, let's use zero-copy.
            //  There's no disadvantage to it as we cannot stuck multiple
            //  messages into the buffer anyway. Note that subsequent
            //  write(s) are non-blocking, thus each single write writes
            //  at most SO_SNDBUF bytes at once not depending on how large
            //  is the chunk returned from here.
            //  As a consequence, large messages being sent won't block
            //  other engines running in the same I/O thread for excessive
            //  amounts of time.
            if (pos == 0 && data == null && to_write >= buffersize)
            {
                data = write_pos;
                size = to_write;

                write_pos = null;
                to_write = 0;
                return;
            }

            //  Copy data to the buffer. If the buffer is full, return.
            int to_copy = Math.Min(to_write, buffersize - pos);

            if (to_copy != 0)
            {
                write_pos.CopyTo(0,buffer, pos, to_copy);
                pos += to_copy;
                write_pos.AdvanceOffset(to_copy);
                to_write -= to_copy;
            }
        }

        data = buffer;
        size = pos;
    }

    protected int state
    {
        get;
        private set;
    }

    protected void encoding_error()
    {
        error = true;
    }

    public bool is_error()
    {
        return error;
    }

    abstract protected bool isNext();

    //protected void next_step (Msg msg_, int state_, bool beginning_) {
    //    if (msg_ == null)
    //        next_step((ByteBuffer) null, 0, state_, beginning_);
    //    else
    //        next_step(msg_.data(), msg_.size(), state_, beginning_);
    //}

    protected void next_step(ByteArraySegment write_pos_, int to_write_,
            int next_, bool beginning_)
    {

        write_pos = write_pos_;
        to_write = to_write_;
        state = next_;
        beginning = beginning_;
    }

    //protected void next_step (byte[] buf_, int to_write_,
    //        int next_, bool beginning_)
    //{
    //    write_buf = null;
    //    write_array = buf_;
    //    write_pos = 0;
    //    to_write = to_write_;
    //    next = next_;
    //    beginning = beginning_;
    //}

}
