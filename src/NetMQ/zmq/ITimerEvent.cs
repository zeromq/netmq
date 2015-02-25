namespace NetMQ.zmq
{
    internal interface ITimerEvent
    {
        // Called when timer expires.
        void TimerEvent(int id);     
    }
}
