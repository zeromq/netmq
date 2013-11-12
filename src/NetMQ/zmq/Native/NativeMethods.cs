using System;
using System.Runtime.InteropServices;

namespace NetMQ.zmq.Native
{
	public static class NativeMethods
	{
		[DllImport("ws2_32.dll", SetLastError = true)]
		internal static extern int select([In] int ignoredParameter, [In, Out] IntPtr[] readfds, [In, Out] IntPtr[] writefds, [In, Out] IntPtr[] exceptfds, [In] ref TimeValue timeout);

		[DllImport("ws2_32.dll", SetLastError = true)]
		internal static extern int select([In] int ignoredParameter, [In, Out] IntPtr[] readfds, [In, Out] IntPtr[] writefds, [In, Out] IntPtr[] exceptfds, [In] IntPtr nullTimeout);

		[StructLayout(LayoutKind.Sequential)]
		internal struct TimeValue
		{
			public TimeValue(int seconds, int microseconds)
			{
				Seconds = seconds;
				Microseconds = microseconds;
			}

			public int Seconds;
			public int Microseconds;
		}
	}
}
