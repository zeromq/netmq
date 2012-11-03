using System;
using NetMQ;

public interface IEncoder
{
    //  Set message producer.
    void set_msg_source (IMsgSource msg_source_);

    //  The function returns a batch of binary data. The data
    //  are filled to a supplied buffer. If no buffer is supplied (data_
    //  is nullL) encoder will provide buffer of its own.
    void get_data(ref ArraySegment<byte> data);
}
