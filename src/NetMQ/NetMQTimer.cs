using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
	public class NetMQTimerEventArgs : EventArgs
	{
		public NetMQTimerEventArgs(NetMQTimer timer)
		{
			Timer = timer;
		}

		public NetMQTimer Timer { get; private set; }
	}

	public class NetMQTimer
	{
		private NetMQTimerEventArgs m_timerEventArgs;
		private TimeSpan m_interval;
		private bool m_enable;

		public event EventHandler<NetMQTimerEventArgs> Elapsed;

		internal event EventHandler<NetMQTimerEventArgs> TimerChanged;

		public NetMQTimer(TimeSpan interval)
		{
			Interval = interval;
			m_timerEventArgs = new NetMQTimerEventArgs(this);

			Cancelled = true;
			Enable = true;
		}

		public TimeSpan Interval
		{
			get { return m_interval; }
			set
			{
				m_interval = value;
				InvokeTimerChanged();
			}
		}

		/// <summary>
		/// If timer attached to a poller Cancelled is false, if cancel is not attached or removed from a poller Cancelled is true
		/// </summary>
		public bool Cancelled { get; internal set; }

		public bool Enable
		{
			get { return m_enable; }
			set
			{
				if (!m_enable.Equals(value))
				{
					m_enable = value;
					InvokeTimerChanged();
				}
			}
		}

		internal void InvokeElapsed(object sender)
		{
			var temp = Elapsed;
			if (temp != null)
			{
				temp(sender, m_timerEventArgs);
			}
		}

		internal void InvokeTimerChanged()
		{
			var temp = TimerChanged;
			if (temp != null)
			{
				temp(this, m_timerEventArgs);
			}
		}
	}
}
