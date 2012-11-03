using System;
using NetMQ;


public class V1Decoder : DecoderBase
{
    
    private const int one_byte_size_ready = 0;
    private const int eight_byte_size_ready = 1;
    private const int flags_ready = 2;
    private const int message_ready = 3;
    
    private byte[] tmpbuf;
    private Msg in_progress;
    private IMsgSink msg_sink;
    private long maxmsgsize;
    private int msg_flags;
    
    public V1Decoder (int bufsize_, long maxmsgsize_, IMsgSink session) : base(bufsize_)
    {

        maxmsgsize = maxmsgsize_;
        msg_sink = session;
        
        tmpbuf = new byte[8];
        
        //  At the beginning, read one byte and go to one_byte_size_ready state.
        next_step(tmpbuf, 1, flags_ready);
    }

    //  Set the receiver of decoded messages.
    public override void set_msg_sink (IMsgSink msg_sink_) 
    {
        msg_sink = msg_sink_;
    }

    
    protected override bool IsNext() {
        switch(state) {
        case one_byte_size_ready:
            return get_one_byte_size_ready ();
        case eight_byte_size_ready:
            return get_eight_byte_size_ready ();
        case flags_ready:
            return get_flags_ready ();
        case message_ready:
            return get_message_ready ();
        default:
            return false;
        }
    }



    private bool get_one_byte_size_ready() {
        
        //  Message size must not exceed the maximum allowed size.
        if (maxmsgsize >= 0)
            if (tmpbuf [0] > maxmsgsize) {
                decoding_error ();
                return false;
            }

        //  in_progress is initialised at this point so in theory we should
        //  close it before calling zmq_msg_init_size, however, it's a 0-byte
        //  message and thus we can treat it as uninitialised...
        in_progress = new Msg(tmpbuf [0]);

        in_progress.SetFlags (msg_flags);
        next_step (in_progress.get_data (), in_progress.size ,message_ready);

        return true;
    }
    
    private bool get_eight_byte_size_ready() {
        
        //  The payload size is encoded as 64-bit unsigned integer.
        //  The most significant byte comes first.        
        long msg_size = BitConverter.ToInt64(tmpbuf, 0);

        //  Message size must not exceed the maximum allowed size.
        if (maxmsgsize >= 0)
            if (msg_size > maxmsgsize) {
                decoding_error ();
                return false;
            }

        //  Message size must fit within range of size_t data type.
        if (msg_size > int.MaxValue) {
            decoding_error ();
            return false;
        }
        
        //  in_progress is initialised at this point so in theory we should
        //  close it before calling init_size, however, it's a 0-byte
        //  message and thus we can treat it as uninitialised.
        in_progress = new Msg ((int) msg_size);

        in_progress.SetFlags (msg_flags);
        next_step (in_progress.get_data (), in_progress.size, message_ready);

        return true;
    }
    
    private bool get_flags_ready() {

        //  Store the flags from the wire into the message structure.
        msg_flags = 0;
        int first = tmpbuf[0];
        if ((first & V1Protocol.MORE_FLAG) > 0)
            msg_flags |= Msg.more;
        
        //  The payload length is either one or eight bytes,
        //  depending on whether the 'large' bit is set.
        if ((first & V1Protocol.LARGE_FLAG) > 0)
            next_step (tmpbuf, 8, eight_byte_size_ready);
        else
            next_step (tmpbuf,1, one_byte_size_ready);
        
        return true;

    }
    
    private bool get_message_ready() {
        //  Message is completely read. Push it further and start reading
        //  new message. (in_progress is a 0-byte message after this point.)
        
        if (msg_sink == null)
            return false;
        
        bool rc = msg_sink.push_msg (in_progress);
        if (!rc) {
            if (ZError.IsError (ZError.EAGAIN))
                decoding_error ();
            
            return false;
        }

        next_step(tmpbuf, 1, flags_ready);
        
        return true;
    }


    //  Returns true if there is a decoded message
    //  waiting to be delivered to the session.
    public override bool stalled ()
    {
        return state == message_ready;
    }

}
