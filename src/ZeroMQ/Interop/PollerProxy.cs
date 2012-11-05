namespace ZeroMQ.Interop
{
    internal class PollerProxy
    {
        public int Poll(PollItem[] pollItems, int timeoutMilliseconds)
        {
            return LibZmq.zmq_poll(pollItems, pollItems.Length, timeoutMilliseconds * LibZmq.PollTimeoutRatio);
        }
    }
}
