using zmq;

namespace ZeroMQ.Monitoring
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;

	/// <summary>
	/// Monitors state change events on another socket within the same context.
	/// </summary>
	public class ZmqMonitor : IDisposable
	{
		/// <summary>
		/// The polling interval in milliseconds.
		/// </summary>
		private const int PollingIntervalMsec = 500;

		private readonly ZmqSocket _socket;
		private readonly string _endpoint;
		private readonly Dictionary<SocketEvent, Action<MonitorEvent>> _eventHandler;

		private volatile bool _isRunning;

		private bool _disposed;

		internal ZmqMonitor(ZmqSocket socket, string endpoint)
		{
			_socket = socket;
			_endpoint = endpoint;
			_eventHandler = new Dictionary<SocketEvent, Action<MonitorEvent>>
            {
                { SocketEvent.Connected, data => InvokeEvent(Connected, () => new ZmqMonitorFileDescriptorEventArgs(this, data)) },
                { SocketEvent.ConnectDelayed, data => InvokeEvent(ConnectDelayed, () => new ZmqMonitorErrorEventArgs(this, data)) },
                { SocketEvent.ConnectRetried, data => InvokeEvent(ConnectRetried, () => new ZmqMonitorIntervalEventArgs(this, data)) },
                { SocketEvent.Listening, data => InvokeEvent(Listening, () => new ZmqMonitorFileDescriptorEventArgs(this, data)) },
                { SocketEvent.BindFailed, data => InvokeEvent(BindFailed, () => new ZmqMonitorErrorEventArgs(this, data)) },
                { SocketEvent.Accepted, data => InvokeEvent(Accepted, () => new ZmqMonitorFileDescriptorEventArgs(this, data)) },
                { SocketEvent.AcceptFailed, data => InvokeEvent(AcceptFailed, () => new ZmqMonitorErrorEventArgs(this, data)) },
                { SocketEvent.Closed, data => InvokeEvent(Closed, () => new ZmqMonitorFileDescriptorEventArgs(this, data)) },
                { SocketEvent.CloseFailed, data => InvokeEvent(CloseFailed, () => new ZmqMonitorErrorEventArgs(this, data)) },
                { SocketEvent.Disconnected, data => InvokeEvent(Disconnected, () => new ZmqMonitorFileDescriptorEventArgs(this, data)) }
            };
		}

		/// <summary>
		/// Occurs when a new connection is established.
		/// </summary>
		/// <remarks>
		/// NOTE: Do not rely on the <see cref="ZmqMonitorEventArgs.Address"/> value for
		/// 'Connected' messages, as the memory address contained in the message may no longer
		/// point to the correct value.
		/// </remarks>
		public event EventHandler<ZmqMonitorFileDescriptorEventArgs> Connected;

		/// <summary>
		/// Occurs when a synchronous connection attempt failed, and its completion is being polled for.
		/// </summary>
		public event EventHandler<ZmqMonitorErrorEventArgs> ConnectDelayed;

		/// <summary>
		/// Occurs when an asynchronous connect / reconnection attempt is being handled by a reconnect timer.
		/// </summary>
		public event EventHandler<ZmqMonitorIntervalEventArgs> ConnectRetried;

		/// <summary>
		/// Occurs when a socket is bound to an address and is ready to accept connections.
		/// </summary>
		public event EventHandler<ZmqMonitorFileDescriptorEventArgs> Listening;

		/// <summary>
		/// Occurs when a socket could not bind to an address.
		/// </summary>
		public event EventHandler<ZmqMonitorErrorEventArgs> BindFailed;

		/// <summary>
		/// Occurs when a connection from a remote peer has been established with a socket's listen address.
		/// </summary>
		public event EventHandler<ZmqMonitorFileDescriptorEventArgs> Accepted;

		/// <summary>
		/// Occurs when a connection attempt to a socket's bound address fails.
		/// </summary>
		public event EventHandler<ZmqMonitorErrorEventArgs> AcceptFailed;

		/// <summary>
		/// Occurs when a connection was closed.
		/// </summary>
		/// <remarks>
		/// NOTE: Do not rely on the <see cref="ZmqMonitorEventArgs.Address"/> value for
		/// 'Closed' messages, as the memory address contained in the message may no longer
		/// point to the correct value.
		/// </remarks>
		public event EventHandler<ZmqMonitorFileDescriptorEventArgs> Closed;

		/// <summary>
		/// Occurs when a connection couldn't be closed.
		/// </summary>
		public event EventHandler<ZmqMonitorErrorEventArgs> CloseFailed;

		/// <summary>
		/// Occurs when the stream engine (tcp and ipc specific) detects a corrupted / broken session.
		/// </summary>
		public event EventHandler<ZmqMonitorFileDescriptorEventArgs> Disconnected;

		/// <summary>
		/// Gets the endpoint to which the monitor socket is connected.
		/// </summary>
		public string Endpoint
		{
			get { return _endpoint; }
		}

		/// <summary>
		/// Gets a value indicating whether the monitor loop is running.
		/// </summary>
		public bool IsRunning
		{
			get { return _isRunning; }
			private set { _isRunning = value; }
		}

		/// <summary>
		/// Begins monitoring for state changes, raising the appropriate events as they arrive.
		/// </summary>
		/// <remarks>NOTE: This is a blocking method and should be run from another thread.</remarks>
		public void Start()
		{
			_socket.Connect(_endpoint);
			
			while (IsRunning)
			{
				var eventData = MonitorEvent.Read(_socket.SocketProxy);
				OnMonitor(ref eventData);
			}
		}

		public void ReceiveEvent()
		{
			var eventData = MonitorEvent.Read(_socket.SocketProxy);
			OnMonitor(ref eventData);
		}

		/// <summary>
		/// Stops monitoring for state changes.
		/// </summary>
		public void Stop()
		{
			IsRunning = false;
		}

		/// <summary>
		/// Releases all resources used by the current instance of the <see cref="ZmqMonitor"/> class.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		internal void OnMonitor(ref MonitorEvent data)
		{
			_eventHandler[data.Event](data);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ZmqMonitor"/>, and optionally disposes of the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					Stop();
					_socket.Dispose();
				}
			}

			_disposed = true;
		}

		private void InvokeEvent<T>(EventHandler<T> handler, Func<T> createEventArgs) where T : EventArgs
		{
			if (handler != null)
			{
				handler(this, createEventArgs());
			}
		}
	}
}