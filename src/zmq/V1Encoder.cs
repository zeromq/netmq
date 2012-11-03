using System;
using NetMQ;

// Encoder for 0MQ framing protocol. Converts messages into data stream.

public class V1Encoder : EncoderBase
{
    private const int size_ready = 0;
    private const int message_ready = 1;
    
    private Msg in_progress;
    private byte[] tmpbuf;
    private ArraySegment<byte> tmpbufArraySegment;

    private ArraySegment<byte> nullArraySegment = new ArraySegment<byte>(new byte[1], 0,0);


    private IMsgSource msg_source;
    
    public V1Encoder (int bufsize_, IMsgSource session) : base(bufsize_)
    {
        tmpbuf = new byte [9];
        msg_source = session;

        tmpbufArraySegment = new ArraySegment<byte>(tmpbuf);

        //  Write 0 bytes to the batch and go to message_ready state.
        next_step(tmpbufArraySegment, 0, message_ready, true);
    }


    public override void set_msg_source (IMsgSource msg_source_)
    {
        msg_source = msg_source_;
    }

    protected override bool isNext() 
    {
        switch(state) {
        case size_ready:
            return get_size_ready ();
        case message_ready:
            return get_message_ready ();
        default:
            return false;
        }
    }

    private ArraySegment<byte> GetMsgArraySegment(Msg msg)
    {
        byte[] data = msg.get_data();

        if (data != null)
        {
            return new ArraySegment<byte>(data);
        }
        else
        {
            return nullArraySegment;
        }
    }
    
    private bool get_size_ready ()
    {      
        //  Write message body into the buffer.
        next_step(GetMsgArraySegment(in_progress), in_progress.size,
            message_ready, !in_progress.has_more());
        return true;
    }


    private bool get_message_ready()
    {
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

        int protocol_flags = 0;
        if (in_progress.has_more ())
            protocol_flags |= V1Protocol.MORE_FLAG;
        if (in_progress.size > 255)
            protocol_flags |= V1Protocol.LARGE_FLAG;
        tmpbuf [0] = (byte) protocol_flags;
        
        //  Encode the message length. For messages less then 256 bytes,
        //  the length is encoded as 8-bit unsigned integer. For larger
        //  messages, 64-bit unsigned integer in network byte order is used.
        int size = in_progress.size;
        if (size > 255) {
            Buffer.BlockCopy(BitConverter.GetBytes((long)size), 0, tmpbuf, 1, 8);
                        
            next_step (tmpbufArraySegment, 9, size_ready, false);
        }
        else {
            tmpbuf [1] = (byte) (size);
            next_step(tmpbufArraySegment, 2, size_ready, false);
        }
        return true;
    }

}
