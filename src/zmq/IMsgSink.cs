using System;

public interface IMsgSink
{
    //  Delivers a message. Returns true if successful; false otherwise.
    //  The function takes ownership of the passed message.
    bool push_msg (Msg msg_);
}
