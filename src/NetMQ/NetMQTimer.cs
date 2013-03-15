using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

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
		private int m_interval;
		private bool m_enable;

		public event EventHandler<NetMQTimerEventArgs> Elapsed;

		public NetMQTimer(TimeSpan interval) : this((int)interval.TotalMilliseconds)
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

				if (Enable)
				{
					When = Clock.NowMs() + Interval;
				}
				else
				{
					When = -1;	
				}				
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
					
					if (m_enable)
					{
						When = Clock.NowMs() + Interval;
					}
					else
					{
						When = -1;
					}
				}
			}
		}

		internal Int64 When { get; set; }

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
