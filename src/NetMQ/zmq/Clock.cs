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
using System.Diagnostics;
using NetMQ.zmq.Native;


namespace NetMQ.zmq
{
	public class Clock
	{
		//  TSC timestamp of when last time measurement was made.
		private static long s_lastTsc;

		//  Physical time corresponding to the TSC above (in milliseconds).
		private static long s_lastTime;

		private static bool s_rdtscSupported;

		static Clock()
		{
			try
			{
				if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Unix ||
					Environment.OSVersion.Platform == (PlatformID)128)
				{
					Opcode.Open();
					s_rdtscSupported = true;
				}
				else
				{
					s_rdtscSupported = false;
				}
			}
			catch (Exception)
			{
				s_rdtscSupported = false;
			}
		}


		//  High precision timestamp.
		public static long NowUs()
		{
			long ticksPerSecond = Stopwatch.Frequency;
			long tick = Stopwatch.GetTimestamp();

			double ticks_div = ticksPerSecond / 1000000.0;
			return (long)(tick / ticks_div);
		}

		//  Low precision timestamp. In tight loops generating it can be
		//  10 to 100 times faster than the high precision timestamp.
		public static long NowMs()
		{
			long tsc = Rdtsc();

			if (tsc == 0)
			{
				return NowUs() / 1000;
			}

			if (tsc - s_lastTsc <= Config.ClockPrecision / 2 && tsc >= s_lastTsc)
			{
				return s_lastTime;
			}

			s_lastTsc = tsc;
			s_lastTime = NowUs() / 1000;
			return s_lastTime;
		}

		//  CPU's timestamp counter. Returns 0 if it's not available.
		public static long Rdtsc()
		{
			if (s_rdtscSupported)
			{
				return (long)Opcode.Rdtsc();
			}
			else
			{
				return 0;
			}
		}
	}
}
