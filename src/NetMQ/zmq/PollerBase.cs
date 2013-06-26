/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NetMQ.zmq
{
	abstract public class PollerBase
	{

		//  Load of the poller. Currently the number of file descriptors
		//  registered.
		private int m_load;

		private class TimerInfo
		{
			public TimerInfo(IPollEvents sink, int id)
			{
				Sink = sink;
				Id = id;
			}

			public IPollEvents Sink { get; private set; }
			public int Id { get; private set; }
		}

		private readonly SortedList<long, List<TimerInfo>> m_timers;

		protected PollerBase()
		{
			m_timers = new SortedList<long, List<TimerInfo>>();
		}

		//  Returns load of the poller. Note that this function can be
		//  invoked from a different thread!
		public int Load
		{
			get
			{
				Thread.MemoryBarrier();
				return m_load;
			}
		}

		//  Called by individual poller implementations to manage the load.
		protected void AdjustLoad(int amount)
		{
			Interlocked.Add(ref m_load, amount); 
		}

		//  Add a timeout to expire in timeout_ milliseconds. After the
		//  expiration timer_event on sink_ object will be called with
		//  argument set to id_.
		public void AddTimer(long timeout, IPollEvents sink, int id)
		{
			long expiration = Clock.NowMs() + timeout;
			TimerInfo info = new TimerInfo(sink, id);

			if (!m_timers.ContainsKey(expiration))
				m_timers.Add(expiration, new List<TimerInfo>());

			m_timers[expiration].Add(info);
		}

		//  Cancel the timer created by sink_ object with ID equal to id_.
		public void CancelTimer(IPollEvents sink, int id)
		{

			//  Complexity of this operation is O(n). We assume it is rarely used.
			var foundTimers = new Dictionary<long, TimerInfo>();

			foreach (var pair in m_timers)
			{
				var timer = pair.Value.FirstOrDefault(x => x.Id == id && x.Sink == sink);

				if (timer == null)
					continue;

				if (!foundTimers.ContainsKey(pair.Key))
				{
					foundTimers[pair.Key] = timer;
					break;
				}
			}

			if (foundTimers.Count > 0)
			{
				foreach (var foundTimer in foundTimers)
				{
					if (m_timers[foundTimer.Key].Count == 1)
					{
						m_timers.Remove(foundTimer.Key);
					}
					else
					{
						m_timers[foundTimer.Key].Remove(foundTimer.Value);
					}
				}
			}
			else
			{
				//  Timer not found.
				Debug.Assert(false);
			}
		}

		//  Executes any timers that are due. Returns number of milliseconds
		//  to wait to match the next timer or 0 meaning "no timers".
		protected int ExecuteTimers()
		{
			//  Fast track.
			if (!m_timers.Any())
				return 0;

			//  Get the current time.
			long current = Clock.NowMs();
					
			//  Execute the timers that are already due.
			var keys = m_timers.Keys;

			for (int i = 0; i < keys.Count; i++)
			{
				var key = keys[i];

				//  If we have to wait to execute the item, same will be true about
				//  all the following items (multimap is sorted). Thus we can stop
				//  checking the subsequent timers and return the time to wait for
				//  the next timer (at least 1ms).
				if (key > current)
				{
					return (int)(key - current);
				}

				var timers = m_timers[key];

				//  Trigger the timers.                
				foreach (var timer in timers)
				{
					timer.Sink.TimerEvent(timer.Id);
				}

				//  Remove it from the list of active timers.		
				timers.Clear();
				m_timers.Remove(key);
				i--;
			}						
			
			//  There are no more timers.
			return 0;
		}
	}
}
