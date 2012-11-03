using System;
using NetMQ;
using zmq;


public interface IDecoder
{
    void set_msg_sink (IMsgSink msg_sink);

    void get_buffer(ref ByteArraySegment data_, ref int size);

    int process_buffer(ByteArraySegment data, int size);
    
    bool stalled ();
}
