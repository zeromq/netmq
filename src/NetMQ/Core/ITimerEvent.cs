namespace NetMQ.Core
{
    /// <summary>
    /// The ITimerEvent interface mandates a TimerEvent( int id ) method,
    /// that is called with an id-value when the timer expires.
    /// </summary>
    internal interface ITimerEvent
    {
        /// <summary>
        /// This is called when the timer expires.
        /// </summary>
        /// <param name="id">an integer used to identify the timer</param>
        void TimerEvent(int id);
    }
}
