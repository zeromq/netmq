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

using Wintellect.PowerCollections;
using System.Diagnostics;
using System.Linq;

namespace NetMQ.zmq
{
	abstract public class PollerBase {

		//  Load of the poller. Currently the number of file descriptors
		//  registered.
		private readonly AtomicInteger m_load;
    
		private class TimerInfo {
			public TimerInfo(IPollEvents sink, int id)
			{
				Sink = sink;
				Id = id;
			}
			
			public IPollEvents Sink { get; private set; }
			public int Id { get; private set; }        			
		}

		private readonly MultiDictionary<long, TimerInfo> m_timers;
    
		protected PollerBase() {
			m_load = new AtomicInteger(0);
			m_timers = new MultiDictionary<long, TimerInfo>(true);
		}
    
		//  Returns load of the poller. Note that this function can be
		//  invoked from a different thread!
		public int Load 
		{
			get { return m_load.Get(); }
		}

		//  Called by individual poller implementations to manage the load.
		protected void AdjustLoad (int amount)
		{       
			m_load.AddAndGet(amount);
		}    
       
		//  Add a timeout to expire in timeout_ milliseconds. After the
		//  expiration timer_event on sink_ object will be called with
		//  argument set to id_.
		public void AddTimer (long timeout, IPollEvents sink, int id)
		{
			long expiration = Clock.NowMs () + timeout;
			TimerInfo info = new TimerInfo(sink, id);
			m_timers.Add(expiration, info);
		}

		//  Cancel the timer created by sink_ object with ID equal to id_.
		public void CancelTimer(IPollEvents sink, int id) {

			//  Complexity of this operation is O(n). We assume it is rarely used.

			var foundTimers = m_timers.KeyValuePairs.Where(it => it.Value.Sink == sink && it.Value.Id == id);

			if (foundTimers.Any())
			{
				m_timers.Remove(foundTimers.First().Key, foundTimers.First().Value);
			}
			else
			{
				//  Timer not found.
				Debug.Assert(false);
			}
		}

		//  Executes any timers that are due. Returns number of milliseconds
		//  to wait to match the next timer or 0 meaning "no timers".
		protected int ExecuteTimers ()
		{
			//  Fast track.
			if (!m_timers.Any ())
				return 0;

			//  Get the current time.
			long current = Clock.NowMs ();

			//   Execute the timers that are already due.

			foreach (var o in m_timers.KeyValuePairs.ToList())
			{                      
				//  If we have to wait to execute the item, same will be true about
				//  all the following items (multimap is sorted). Thus we can stop
				//  checking the subsequent timers and return the time to wait for
				//  the next timer (at least 1ms).
				if (o.Key > current)
				{
					return  (int)(o.Key - current);                
				}
				//  Trigger the timer.
				o.Value.Sink.TimerEvent (o.Value.Id);

				//  Remove it from the list of active timers.
				//timers_t::iterator o = it;
				//++it;
				//timers.erase (o);


            
				m_timers.Remove(o.Key, o.Value);            
			}

        

			//  There are no more timers.
			return 0;
		}
	}
}
