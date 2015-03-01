using System;
using JetBrains.Annotations;
using NetMQ.zmq.Utils;

namespace NetMQ
{
    /// <summary>
    /// Class NetMQTimerEventArgs is an EventArgs that contains a reference to a NetMQTimer.
    /// </summary>
    public class NetMQTimerEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQTimerEventArgs that contains a reference to the given NetMQTimer.
        /// </summary>
        /// <param name="timer">the NetMQTimer to hold a reference to</param>
        public NetMQTimerEventArgs([NotNull] NetMQTimer timer)
        {
            Timer = timer;
        }

        /// <summary>
        /// Get the NetMQTimer that this has a reference to.
        /// </summary>
        [NotNull]
        public NetMQTimer Timer { get; private set; }
    }

    public class NetMQTimer
    {
        private readonly NetMQTimerEventArgs m_timerEventArgs;

        /// <summary>
        /// This is the timer-interval in milliseconds.
        /// </summary>
        private int m_interval;

        private bool m_enable;

        /// <summary>
        /// This event is used to signal when the timer has expired.
        /// </summary>
        public event EventHandler<NetMQTimerEventArgs> Elapsed;

        /// <summary>
        /// Create a new NetMQTimer with the timer-interval specified by the given TimeSpan.
        /// </summary>
        /// <param name="interval">a TimeSpan that denotes the timer-interval</param>
        public NetMQTimer(TimeSpan interval)
            : this((int)interval.TotalMilliseconds)
        {}

        /// <summary>
        /// Create a new NetMQTimer with the given timer-interval in milliseconds.
        /// </summary>
        /// <param name="interval">an integer specifying the timer-interval in milliseconds</param>
        public NetMQTimer(int interval)
        {
            m_interval = interval;
            m_timerEventArgs = new NetMQTimerEventArgs(this);

            m_enable = true;

            When = -1;
        }

        /// <summary>
        /// Get or set the timer-interval, in milliseconds.
        /// </summary>
        public int Interval
        {
            get { return m_interval; }
            set
            {
                m_interval = value;

                When = Enable ? Clock.NowMs() + Interval : -1;
            }
        }

        /// <summary>
        /// Get or set whether this NetMQTimer is on.
        /// </summary>
        public bool Enable
        {
            get { return m_enable; }
            set
            {
                if (!m_enable.Equals(value))
                {
                    m_enable = value;

                    When = m_enable ? Clock.NowMs() + Interval : -1;
                }
            }
        }

        /// <summary>
        /// Get or set the value of the low-precision timestamp that signals when the timer is to expire.
        /// </summary>
        internal long When { get; set; }

        /// <summary>
        /// If there are any subscribers - raise the Elapsed event.
        /// </summary>
        /// <param name="sender">the sender to include within the event's event-args</param>
        internal void InvokeElapsed(object sender)
        {
            var temp = Elapsed;
            if (temp != null)
            {
                temp(sender, m_timerEventArgs);
            }
        }
    }
}
