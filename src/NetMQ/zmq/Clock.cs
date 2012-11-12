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
using System.Runtime.InteropServices;

namespace NetMQ.zmq
{
	public class Clock
	{

		private static readonly DateTime Jan1St1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		//  TSC timestamp of when last time measurement was made.
		 private static long s_lastTsc;

		//  Physical time corresponding to the TSC above (in milliseconds).
		 private static long s_lastTime;

		[Flags()]
		public enum AllocationType : uint
		{
			COMMIT = 0x1000,
			RESERVE = 0x2000,
			RESET = 0x80000,
			LARGE_PAGES = 0x20000000,
			PHYSICAL = 0x400000,
			TOP_DOWN = 0x100000,
			WRITE_WATCH = 0x200000
		}

		[Flags()]
		public enum MemoryProtection : uint
		{
			EXECUTE = 0x10,
			EXECUTE_READ = 0x20,
			EXECUTE_READWRITE = 0x40,
			EXECUTE_WRITECOPY = 0x80,
			NOACCESS = 0x01,
			READONLY = 0x02,
			READWRITE = 0x04,
			WRITECOPY = 0x08,
			GUARD = 0x100,
			NOCACHE = 0x200,
			WRITECOMBINE = 0x400
		}

		[Flags]
		public enum FreeType
		{
			DECOMMIT = 0x4000,
			RELEASE = 0x8000
		}

		private static class NativeMethods
		{
			private const string KERNEL = "kernel32.dll";

			[DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
			public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
				AllocationType flAllocationType, MemoryProtection flProtect);

			[DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
			public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize,
				FreeType dwFreeType);
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate ulong GetTickDelegate();
		static readonly IntPtr Addr;
		static readonly GetTickDelegate getTick;


		private static readonly byte[] RDTSC_32 = new byte[] {
      0x0F, 0x31,                     // rdtsc   
      0xC3                            // ret  
    };

		private static readonly byte[] RDTSC_64 = new byte[] {
      0x0F, 0x31,                     // rdtsc  
      0x48, 0xC1, 0xE2, 0x20,         // shl rdx, 20h  
      0x48, 0x0B, 0xC2,               // or rax, rdx  
      0xC3                            // ret  
    };

		static Clock()
		{
			byte[] rdtscCode;

			if (IntPtr.Size == 4)
			{
				rdtscCode = RDTSC_32;
			}
			else
			{
				rdtscCode = RDTSC_64;
			}

			Addr = NativeMethods.VirtualAlloc(IntPtr.Zero,
				 (UIntPtr)rdtscCode.Length, AllocationType.COMMIT | AllocationType.RESERVE,
				 MemoryProtection.EXECUTE_READWRITE);

			Marshal.Copy(rdtscCode, 0, Addr, rdtscCode.Length);

			getTick = (GetTickDelegate)Marshal.GetDelegateForFunctionPointer(Addr, typeof(GetTickDelegate));

			try
			{
				getTick();
			}
			catch(Exception)
			{
				getTick = null;
				NativeMethods.VirtualFree(Addr, UIntPtr.Zero, FreeType.RELEASE);
			}
		}


		//  High precision timestamp.
		public static long NowUs()
		{
			long ticksPerSecond = Stopwatch.Frequency;
			long tick = Stopwatch.GetTimestamp();

			double ticks_div = (double)(ticksPerSecond / 1000000);
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
			
			if (tsc - s_lastTsc <=  Config.ClockPrecision / 2 && tsc >= s_lastTsc)
			{
				return s_lastTime;
			}

			s_lastTsc = tsc;
			s_lastTime = NowUs()/1000;
			return s_lastTime;
		}

		//  CPU's timestamp counter. Returns 0 if it's not available.
		public static long Rdtsc()
		{
			if (getTick != null)
			{
				return (long) getTick();
			}
			else
			{
				return 0;
			}
		}
	}
}
