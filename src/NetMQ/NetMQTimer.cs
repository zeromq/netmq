using System;
using JetBrains.Annotations;
using NetMQ.Core.Utils;

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
        public NetMQTimer Timer { get; }
    }

    /// <summary>
    /// A NetMQTimer instance provides the state-information for a timer function,
    /// which is periodically checked by a Poller or a NetMQBeacon.
    /// </summary>
    public class NetMQTimer
    {
        /// <summary>
        /// A pre-constructed NetMQTimerEventArgs to use whenever raising the Elapsed event.
        /// </summary>
        private readonly NetMQTimerEventArgs m_timerEventArgs;

        /// <summary>
        /// This is the timer-interval in milliseconds.
        /// </summary>
        private int m_interval;

        /// <summary>
        /// This flag dictates whether this timer is currently enabled.
        /// </summary>
        private bool m_enable;

        /// <summary>
        /// This event is used to signal when the timer has expired.
        /// </summary>
        public event EventHandler<NetMQTimerEventArgs> Elapsed;

        /// <summary>
        /// Create a new NetMQTimer with the timer-interval specified by the given TimeSpan.
        /// </summary>
        /// <param name="interval">a TimeSpan that denotes the timer-interval</param>
        /// <remarks>
        /// This sets the When property to an initial value of -1, to indicate it no future-time applies as yet.
        /// </remarks>
        public NetMQTimer(TimeSpan interval)
            : this((int)interval.TotalMilliseconds)
        { }

        /// <summary>
        /// Create a new NetMQTimer with the given timer-interval in milliseconds.
        /// </summary>
        /// <param name="interval">an integer specifying the timer-interval in milliseconds</param>
        /// <remarks>
        /// This sets the When property to an initial value of -1, to indicate it no future-time applies as yet.
        /// </remarks>
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
        /// <remarks>
        /// When setting this, When is set to the future point in time from now at which the interval will expire (or -1 if not Enabled).
        /// </remarks>
        public int Interval
        {
            get => m_interval;
            set
            {
                m_interval = value;
                When = Enable ? Clock.NowMs() + Interval : -1;
            }
        }

        /// <summary>
        /// Get or set whether this NetMQTimer is on.
        /// </summary>
        /// <remarks>
        /// When setting this to true, When is set to the future point in time from now at which the interval will expire.
        /// When setting this to false, When is set to -1.
        /// </remarks>
        public bool Enable
        {
            get => m_enable;
            set
            {
                if (m_enable == value)
                    return;

                m_enable = value;
                When = m_enable ? Clock.NowMs() + Interval : -1;
            }
        }

        /// <summary>
        /// Get or set the value of the low-precision timestamp (a value in milliseconds) that signals when the timer is to expire,
        /// or -1 if not applicable at this time.
        /// </summary>
        internal long When { get; set; }

        /// <summary>
        /// Enable the timer and reset the interval
        /// </summary>
        public void EnableAndReset()
        {
            if (!Enable)
                Enable = true;
            else
                When = Clock.NowMs() + Interval;
        }

        /// <summary>
        /// If there are any subscribers - raise the Elapsed event.
        /// </summary>
        /// <param name="sender">the sender to include within the event's event-args</param>
        internal void InvokeElapsed(object sender)
        {
            Elapsed?.Invoke(sender, m_timerEventArgs);
        }
    }
}
