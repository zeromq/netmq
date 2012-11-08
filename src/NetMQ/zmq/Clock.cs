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

using System;

namespace NetMQ.zmq
{
	public class Clock {

		private static readonly DateTime Jan1St1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		//  TSC timestamp of when last time measurement was made.
		// private long last_tsc;

		//  Physical time corresponding to the TSC above (in milliseconds).
		// private long last_time;
    
		private Clock() {
		}

    
		//  High precision timestamp.
		public static long NowUs() {
			return (long)(System.DateTime.UtcNow - Jan1St1970).TotalMilliseconds * 1000L;
		}

		//  Low precision timestamp. In tight loops generating it can be
		//  10 to 100 times faster than the high precision timestamp.
		public static long NowMs() {
			return (long) (System.DateTime.UtcNow - Jan1St1970).TotalMilliseconds;
		}
    
		//  CPU's timestamp counter. Returns 0 if it's not available.
		public static long Rdtsc() {
			return 0;
		}
	}
}
