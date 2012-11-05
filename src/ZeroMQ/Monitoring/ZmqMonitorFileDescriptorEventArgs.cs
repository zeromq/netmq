using zmq;

namespace ZeroMQ.Monitoring
{
	using System;

	/// <summary>
	/// Provides data for <see cref="ZmqMonitor.Connected"/>, <see cref="ZmqMonitor.Listening"/>, <see cref="ZmqMonitor.Accepted"/>, <see cref="ZmqMonitor.Closed"/> and <see cref="ZmqMonitor.Disconnected"/> events.
	/// </summary>
	public class ZmqMonitorFileDescriptorEventArgs : ZmqMonitorEventArgs
	{
		internal ZmqMonitorFileDescriptorEventArgs(ZmqMonitor monitor, MonitorEvent data)
			: base(monitor, data.Addr)
		{
			FileDescriptor = (IntPtr)(data.Arg);
		}

		/// <summary>
		/// Gets the monitor descriptor.
		/// </summary>

		public IntPtr FileDescriptor { get; private set; }

	}
}