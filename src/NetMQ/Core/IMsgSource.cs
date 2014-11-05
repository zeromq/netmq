namespace NetMQ.Core
{
    interface IMsgSource
    {
        //  Fetch a message. Returns a Msg instance if successful; null otherwise.
        bool PullMsg(ref Msg msg);
    }
}
