namespace NetMQ.zmq
{
    internal interface IMsgSink
    {
        //  Delivers a message. Returns true if successful; false otherwise.
        //  The function takes ownership of the passed message.
        bool PushMsg(ref Msg msg);
    }
}
