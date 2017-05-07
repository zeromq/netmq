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

using System;
using System.Diagnostics;

namespace NetMQ.Core.Utils
{
    /// <summary>
    /// The Clock class provides properties for getting timer-counts in either milliseconds or microseconds,
    /// and the CPU's timestamp-counter if available.
    /// </summary>
    internal static class Clock
    {
        /// <summary>
        /// TSC timestamp of when last time measurement was made.
        /// </summary>
        private static long s_lastTsc;

        /// <summary>
        /// Physical time corresponding to the TSC above (in milliseconds).
        /// </summary>
        private static long s_lastTime;

#if !NETSTANDARD1_3 && !UAP
        /// <summary>
        /// This flag indicates whether the rdtsc instruction is supported on this platform.
        /// </summary>
        private static readonly bool s_rdtscSupported;
#endif

        static Clock()
        {
#if !NETSTANDARD1_3 && !UAP
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT ||
                    Environment.OSVersion.Platform == PlatformID.Unix ||
                    Environment.OSVersion.Platform == (PlatformID)128)
                {
                    s_rdtscSupported = Opcode.Open();
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
#endif
        }

        /// <summary>
        /// Return the High-Precision timestamp, as a 64-bit integer that denotes microseconds.
        /// </summary>
        public static long NowUs()
        {
            long ticksPerSecond = Stopwatch.Frequency;
            long tickCount = Stopwatch.GetTimestamp();

            double ticksPerMicrosecond = ticksPerSecond / 1000000.0;
            return (long)(tickCount / ticksPerMicrosecond);
        }

        /// <summary>
        /// Return the Low-Precision timestamp, as a 64-bit integer denoting milliseconds.
        /// In tight loops generating it can be 10 to 100 times faster than the High-Precision timestamp.
        /// </summary>
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

        /// <summary>
        /// Return the CPU's timestamp counter, or 0 if it's not available.
        /// </summary>
        public static long Rdtsc()
        {
#if NETSTANDARD1_3 || UAP
            return 0;
#else
            return s_rdtscSupported ? (long)Opcode.Rdtsc() : 0;
#endif
        }
    }
}