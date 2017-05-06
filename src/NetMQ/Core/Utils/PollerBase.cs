/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2015 Other contributors as noted in the AUTHORS file

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
using JetBrains.Annotations;

namespace NetMQ.Core.Utils
{
    /// <summary>
    /// This serves as the parent-class for Poller and Proactor.
    /// It provides for managing a list of timers (ITimerEvents) - adding, cancelling, and executing them,
    /// and a Load property.
    /// </summary>
    internal abstract class PollerBase
    {
        /// <summary>
        /// Load of the poller. Currently the number of file descriptors registered.
        /// </summary>
        private int m_load;

        /// <summary>
        /// Instances of this class contain a ITimerEvent sink and an integer Id.
        /// </summary>
        private class TimerInfo
        {
            /// <summary>
            /// Create a new TimerInfo object from the given sink and id.
            /// </summary>
            /// <param name="sink">an ITimerEvent that acts as a sink for when the timer expires</param>
            /// <param name="id">an integer id that identifies this timer</param>
            public TimerInfo([NotNull] ITimerEvent sink, int id)
            {
                Sink = sink;
                Id = id;
            }

            /// <summary>
            /// Get the ITimerEvent that serves as the event-sink.
            /// </summary>
            [NotNull]
            public ITimerEvent Sink { get; }

            /// <summary>
            /// Get the integer Id of this TimerInfo.
            /// </summary>
            public int Id { get; }
        }

        /// <summary>
        /// This is a list of key/value pairs, with the keys being timeout numbers and the corresponding values being a list of TimerInfo objects.
        /// It is sorted by the keys - which are timeout values. Thus, by walking down the list, you encounter the soonest timeouts first.
        /// </summary>
        private readonly SortedList<long, List<TimerInfo>> m_timers;

        /// <summary>
        /// Create a new PollerBase object - which simply creates an empty m_timers collection.
        /// </summary>
        protected PollerBase()
        {
            m_timers = new SortedList<long, List<TimerInfo>>();
        }

        /// <summary>
        /// Get the load of this poller. Note that this function can be
        /// invoked from a different thread!
        /// </summary>
        public int Load
        {
            get
            {
#if NETSTANDARD1_3 || UAP
                return Volatile.Read(ref m_load);
#else
                Thread.MemoryBarrier();
                return m_load;
#endif
            }
        }

        /// <summary>
        /// Add the given amount to the load.
        /// This is called by individual poller implementations to manage the load.
        /// </summary>
        /// <remarks>
        /// This is thread-safe.
        /// </remarks>
        protected void AdjustLoad(int amount)
        {
            Interlocked.Add(ref m_load, amount);
        }

        /// <summary>
        /// Add a <see cref="TimerInfo"/> to the internal list, created from the given sink and id - to expire in the given number of milliseconds.
        /// Afterward the expiration method TimerEvent on the sink object will be called with argument set to id.
        /// </summary>
        /// <param name="timeout">the timeout-period in milliseconds of the new timer</param>
        /// <param name="sink">the IProactorEvents to add for the sink of the new timer</param>
        /// <param name="id">the Id to assign to the new TimerInfo</param>
        public void AddTimer(long timeout, [NotNull] IProactorEvents sink, int id)
        {
            long expiration = Clock.NowMs() + timeout;
            var info = new TimerInfo(sink, id);

            if (!m_timers.ContainsKey(expiration))
                m_timers.Add(expiration, new List<TimerInfo>());

            m_timers[expiration].Add(info);
        }

        /// <summary>
        /// Cancel the timer that was created with the given sink object with the given Id.
        /// </summary>
        /// <param name="sink">the ITimerEvent that the timer was created with</param>
        /// <param name="id">the Id of the timer to cancel</param>
        /// <remarks>
        /// The complexity of this operation is O(n). We assume it is rarely used.
        /// </remarks>
        public void CancelTimer([NotNull] ITimerEvent sink, int id)
        {
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
                // Timer not found.
                Debug.Assert(false);
            }
        }

        /// <summary>
        /// Execute any timers that are due. Return the number of milliseconds
        /// to wait to match the next timer or 0 meaning "no timers".
        /// </summary>
        /// <returns>the time to wait for the next timer, in milliseconds, or zero if there are no more timers</returns>
        protected int ExecuteTimers()
        {
            // Immediately return 0 if there are no timers.
            if (m_timers.Count == 0)
                return 0;

            // Get the current time.
            long current = Clock.NowMs();

            // Execute the timers that are already due.

            // Iterate through all of the timers..
            var keys = m_timers.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];

                // If we have to wait to execute the item, same will be true about
                // all the following items (multimap is sorted). Thus we can stop
                // checking the subsequent timers and return the time to wait for
                // the next timer (at least 1ms).
                if (key > current)
                {
                    return (int)(key - current);
                }

                // We DONT have to wait for this timeout-period, so get the list of timers that correspond to this key.
                var timers = m_timers[key];

                // Trigger the timers.
                foreach (var timer in timers)
                {
                    // "Trigger" each timer by calling it's TimerEvent method with this timer's id.
                    timer.Sink.TimerEvent(timer.Id);
                }

                // Remove it from the list of active timers.
                timers.Clear();
                m_timers.Remove(key);
                i--;
            }

            // There are no more timers.
            return 0;
        }
    }
}