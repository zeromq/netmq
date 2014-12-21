namespace NetMQ
{
	using System;

	using NetMQ.Monitoring;

	public interface INetMQMonitor : IDisposable
	{
		string Endpoint { get; }

		bool IsRunning { get; }

		TimeSpan Timeout { get; set; }

		event EventHandler<NetMQMonitorSocketEventArgs> Accepted;

		event EventHandler<NetMQMonitorErrorEventArgs> AcceptFailed;

		event EventHandler<NetMQMonitorErrorEventArgs> BindFailed;

		event EventHandler<NetMQMonitorSocketEventArgs> Closed;

		event EventHandler<NetMQMonitorErrorEventArgs> CloseFailed;

		event EventHandler<NetMQMonitorErrorEventArgs> ConnectDelayed;

		event EventHandler<NetMQMonitorSocketEventArgs> Connected;

		event EventHandler<NetMQMonitorIntervalEventArgs> ConnectRetried;

		event EventHandler<NetMQMonitorSocketEventArgs> Disconnected;

		event EventHandler<NetMQMonitorSocketEventArgs> Listening;

		void AttachToPoller(IPoller poller);

		void DetachFromPoller();

		void Start();

		void Stop();
	}
}