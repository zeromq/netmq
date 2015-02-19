namespace NetMQ.zmq
{
    public interface ITimerEvent
    {
        // Called when timer expires.
        void TimerEvent(int id);     
    }
}
