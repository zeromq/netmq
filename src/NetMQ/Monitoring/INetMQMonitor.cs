using System;
using System.Threading.Tasks;

namespace NetMQ.Monitoring
{
    /// <summary>
    /// Monitors a <see cref="NetMQSocket"/> for events, raising them via events.
    /// </summary>
    /// <remarks>
    /// To run a monitor instance, either:
    /// <list type="bullet">
    ///   <item>Call <see cref="Start"/> (blocking) and <see cref="Stop"/>, or</item>
    ///   <item>Call <see cref="AttachToPoller{T}"/> and <see cref="DetachFromPoller()"/>.</item>
    /// </list>
    /// </remarks>
    public interface INetMQMonitor : IDisposable
    {
        /// <summary>
        /// Gets the monitoring address.
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// Gets whether this monitor is currently running.
        /// </summary>
        /// <remarks>
        /// Start the monitor running via either <see cref="Start"/> or <see cref="AttachToPoller{T}"/>.
        /// Stop the monitor via either <see cref="Stop"/> or <see cref="DetachFromPoller()"/>.
        /// </remarks>
        bool IsRunning { get; }

        /// <summary>
        /// Gets and sets the timeout interval for poll iterations when using <see cref="Start"/> and <see cref="Stop"/>.
        /// </summary>
        /// <remarks>
        /// The higher the number the longer it may take the to stop the monitor.
        /// This value has no effect when the monitor is run via <see cref="AttachToPoller{T}"/>.
        /// </remarks>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Raised whenever any monitored event fires.
        /// </summary>
        event EventHandler<NetMQMonitorEventArgs>? EventReceived;

        /// <summary>
        /// Occurs when a connection is made to a socket.
        /// </summary>
        event EventHandler<NetMQMonitorSocketEventArgs>? Connected;

        /// <summary>
        /// Occurs when a synchronous connection attempt failed, and its completion is being polled for.
        /// </summary>
        event EventHandler<NetMQMonitorErrorEventArgs>? ConnectDelayed;

        /// <summary>
        /// Occurs when an asynchronous connect / reconnection attempt is being handled by a reconnect timer.
        /// </summary>
        event EventHandler<NetMQMonitorIntervalEventArgs>? ConnectRetried;

        /// <summary>
        /// Occurs when a socket is bound to an address and is ready to accept connections.
        /// </summary>
        event EventHandler<NetMQMonitorSocketEventArgs>? Listening;

        /// <summary>
        /// Occurs when a socket could not bind to an address.
        /// </summary>
        event EventHandler<NetMQMonitorErrorEventArgs>? BindFailed;

        /// <summary>
        /// Occurs when a connection from a remote peer has been established with a socket's listen address.
        /// </summary>
        event EventHandler<NetMQMonitorSocketEventArgs>? Accepted;

        /// <summary>
        /// Occurs when a connection attempt to a socket's bound address fails.
        /// </summary>
        event EventHandler<NetMQMonitorErrorEventArgs>? AcceptFailed;

        /// <summary>
        /// Occurs when a connection was closed.
        /// </summary>
        event EventHandler<NetMQMonitorSocketEventArgs>? Closed;

        /// <summary>
        /// Occurs when a connection couldn't be closed.
        /// </summary>
        event EventHandler<NetMQMonitorErrorEventArgs>? CloseFailed;

        /// <summary>
        /// Occurs when the stream engine (TCP and IPC specific) detects a corrupted / broken session.
        /// </summary>
        event EventHandler<NetMQMonitorSocketEventArgs>? Disconnected;

        /// <summary>
        /// Adds the monitor object to a NetMQPoller. Register to <see cref="EventReceived"/> to be signalled on new events.
        /// </summary>
        /// <param name="poller">The poller to attach to.</param>
        /// <typeparam name="T">The type of poller.</typeparam>
        /// <exception cref="ArgumentNullException">The <paramref name="poller"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The monitor is already started or already attached to a poller.</exception>
        void AttachToPoller<T>(T poller) where T : INetMQPoller;

        /// <summary>
        /// Removes the monitor object from the attached poller.
        /// </summary>
        void DetachFromPoller();

        /// <summary>
        /// Starts monitoring the socket. This method doesn't start a new thread and will block until the monitor poll is stopped.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Monitor must not have already started nor attached to a poller.</exception>
        void Start();

        /// <summary>
        /// Starts a background task for the monitoring operation.
        /// </summary>
        /// <returns>A task representing the monitoring operation.</returns>
        Task StartAsync();

        /// <summary>
        /// Stops monitoring. Blocks until monitoring completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">If this monitor is attached to a poller you must detach it first and not use the <see cref="Stop"/> method.</exception>
        void Stop();
    }
}
