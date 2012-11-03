using System;
using NetMQ;


public interface IDecoder
{
    void set_msg_sink (IMsgSink msg_sink);
    
    void get_buffer (ref ArraySegment<byte> data);
    
    int process_buffer(ArraySegment<byte> data);
    
    bool stalled ();
}
