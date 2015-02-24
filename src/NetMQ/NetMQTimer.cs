using System;
using NetMQ.zmq.Utils;
using JetBrains.Annotations;

namespace NetMQ
{
    public class NetMQTimerEventArgs : EventArgs
    {
        public NetMQTimerEventArgs([NotNull] NetMQTimer timer)
        {
            Timer = timer;
        }

        [NotNull]
        public NetMQTimer Timer { get; private set; }
    }

    public class NetMQTimer
    {
        private readonly NetMQTimerEventArgs m_timerEventArgs;
        private int m_interval;
        private bool m_enable;

        public event EventHandler<NetMQTimerEventArgs> Elapsed;

        public NetMQTimer(TimeSpan interval)
            : this((int)interval.TotalMilliseconds)
        {
        }

        public NetMQTimer(int interval)
        {
            m_interval = interval;
            m_timerEventArgs = new NetMQTimerEventArgs(this);

            m_enable = true;

            When = -1;
        }

        public int Interval
        {
            get { return m_interval; }
            set
            {
                m_interval = value;

                When = Enable ? Clock.NowMs() + Interval : -1;
            }
        }

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

        internal long When { get; set; }

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
