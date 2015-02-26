namespace NetMQ.zmq
{
    internal interface IMsgSink
    {
        /// <summary>
        ///  Delivers a message. Returns true if successful; false otherwise.
        ///  The function takes ownership of the passed message.
        /// </summary>
        /// <param name="msg">the message to deliver</param>
        bool PushMsg(ref Msg msg);
    }
}
