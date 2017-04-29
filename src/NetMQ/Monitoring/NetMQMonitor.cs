using System;
using System.Threading;
#if !NET35
using System.Threading.Tasks;
#endif
using AsyncIO;
using JetBrains.Annotations;
using NetMQ.Core;
using NetMQ.Sockets;

namespace NetMQ.Monitoring
{
    /// <summary>
    /// Monitors a <see cref="NetMQSocket"/> for events, raising them via events.
    /// </summary>
    /// <remarks>
    /// To run a monitor instance, either:
    /// <list type="bullet">
    ///   <item>Call <see cref="Start"/> (blocking) and <see cref="Stop"/>, or</item>
    ///   <item>Call <see cref="AttachToPoller"/> and <see cref="DetachFromPoller"/>.</item>
    /// </list>
    /// </remarks>
    public class NetMQMonitor : IDisposable
    {
        [NotNull] private readonly NetMQSocket m_monitoringSocket;
        private readonly bool m_ownsMonitoringSocket;
        [CanBeNull] private ISocketPollableCollection m_attachedPoller;
        private int m_cancel;

        private readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(true);

        public NetMQMonitor([NotNull] NetMQSocket monitoredSocket, [NotNull] string endpoint, SocketEvents eventsToMonitor)
        {
            Endpoint = endpoint;
            Timeout = TimeSpan.FromSeconds(0.5);

            monitoredSocket.Monitor(endpoint, eventsToMonitor);

            m_monitoringSocket = new PairSocket();
            m_monitoringSocket.Options.Linger = TimeSpan.Zero;
            m_monitoringSocket.ReceiveReady += Handle;

            m_ownsMonitoringSocket = true;
        }

        /// <summary>
        /// Initialises a monitor on <paramref name="socket"/> for a specified <paramref name="endpoint"/>.
        /// </summary>
        /// <remarks>
        /// This constructor matches the signature used by clrzmq.
        /// </remarks>
        /// <param name="socket">The socket to monitor.</param>
        /// <param name="endpoint">a string denoting the endpoint which will be the monitoring address</param>
        /// <param name="ownsSocket">
        /// A flag indicating whether ownership of <paramref name="socket"/> is transferred to the monitor.
        /// If <c>true</c>, disposing the monitor will also dispose <paramref name="socket"/>.
        /// </param>
        public NetMQMonitor([NotNull] NetMQSocket socket, [NotNull] string endpoint, bool ownsSocket = false)
        {
            Endpoint = endpoint;
            Timeout = TimeSpan.FromSeconds(0.5);
            m_monitoringSocket = socket;
            m_monitoringSocket.ReceiveReady += Handle;

            m_ownsMonitoringSocket = ownsSocket;
        }

        /// <summary>
        /// The monitoring address.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Get whether this monitor is currently running.
        /// </summary>
        /// <remarks>
        /// Start the monitor running via either <see cref="Start"/> or <see cref="AttachToPoller"/>.
        /// Stop the monitor via either <see cref="Stop"/> or <see cref="DetachFromPoller"/>.
        /// </remarks>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets and sets the timeout interval for poll iterations when using <see cref="Start"/> and <see cref="Stop"/>.
        /// </summary>
        /// <remarks>
        /// The higher the number the longer it may take the to stop the monitor.
        /// This value has no effect when the monitor is run via <see cref="AttachToPoller"/>.
        /// </remarks>
        public TimeSpan Timeout { get; set; }

        #region Events

        /// <summary>
        /// Raised whenever any monitored event fires.
        /// </summary>
        public event EventHandler<NetMQMonitorEventArgs> EventReceived;

        /// <summary>
        /// Occurs when a connection is made to a socket.
        /// </summary>
        public event EventHandler<NetMQMonitorSocketEventArgs> Connected;

        /// <summary>
        /// Occurs when a synchronous connection attempt failed, and its completion is being polled for.
        /// </summary>
        public event EventHandler<NetMQMonitorErrorEventArgs> ConnectDelayed;

        /// <summary>
        /// Occurs when an asynchronous connect / reconnection attempt is being handled by a reconnect timer.
        /// </summary>
        public event EventHandler<NetMQMonitorIntervalEventArgs> ConnectRetried;

        /// <summary>
        /// Occurs when a socket is bound to an address and is ready to accept connections.
        /// </summary>
        public event EventHandler<NetMQMonitorSocketEventArgs> Listening;

        /// <summary>
        /// Occurs when a socket could not bind to an address.
        /// </summary>
        public event EventHandler<NetMQMonitorErrorEventArgs> BindFailed;

        /// <summary>
        /// Occurs when a connection from a remote peer has been established with a socket's listen address.
        /// </summary>
        public event EventHandler<NetMQMonitorSocketEventArgs> Accepted;

        /// <summary>
        /// Occurs when a connection attempt to a socket's bound address fails.
        /// </summary>
        public event EventHandler<NetMQMonitorErrorEventArgs> AcceptFailed;

        /// <summary>
        /// Occurs when a connection was closed.
        /// </summary>
        public event EventHandler<NetMQMonitorSocketEventArgs> Closed;

        /// <summary>
        /// Occurs when a connection couldn't be closed.
        /// </summary>
        public event EventHandler<NetMQMonitorErrorEventArgs> CloseFailed;

        /// <summary>
        /// Occurs when the stream engine (TCP and IPC specific) detects a corrupted / broken session.
        /// </summary>
        public event EventHandler<NetMQMonitorSocketEventArgs> Disconnected;

        #endregion

        private void Handle(object sender, NetMQSocketEventArgs socketEventArgs)
        {
            var monitorEvent = MonitorEvent.Read(m_monitoringSocket.SocketHandle);

            switch (monitorEvent.Event)
            {
                case SocketEvents.Connected:
                    InvokeEvent(Connected, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (AsyncSocket)monitorEvent.Arg, SocketEvents.Connected));
                    break;
                case SocketEvents.ConnectDelayed:
                    InvokeEvent(ConnectDelayed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg, SocketEvents.ConnectDelayed));
                    break;
                case SocketEvents.ConnectRetried:
                    InvokeEvent(ConnectRetried, new NetMQMonitorIntervalEventArgs(this, monitorEvent.Addr, (int)monitorEvent.Arg, SocketEvents.ConnectRetried));
                    break;
                case SocketEvents.Listening:
                    InvokeEvent(Listening, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (AsyncSocket)monitorEvent.Arg, SocketEvents.Listening));
                    break;
                case SocketEvents.BindFailed:
                    InvokeEvent(BindFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg, SocketEvents.BindFailed));
                    break;
                case SocketEvents.Accepted:
                    InvokeEvent(Accepted, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (AsyncSocket)monitorEvent.Arg, SocketEvents.Accepted));
                    break;
                case SocketEvents.AcceptFailed:
                    InvokeEvent(AcceptFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg, SocketEvents.AcceptFailed));
                    break;
                case SocketEvents.Closed:
                    InvokeEvent(Closed, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (AsyncSocket)monitorEvent.Arg, SocketEvents.Closed));
                    break;
                case SocketEvents.CloseFailed:
                    InvokeEvent(CloseFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg, SocketEvents.CloseFailed));
                    break;
                case SocketEvents.Disconnected:
                    InvokeEvent(Disconnected, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (AsyncSocket)monitorEvent.Arg, SocketEvents.Disconnected));
                    break;
                default:
                    throw new Exception("unknown event " + monitorEvent.Event);
            }
        }

        private void InvokeEvent<T>(EventHandler<T> handler, T args) where T : NetMQMonitorEventArgs
        {
            EventReceived?.Invoke(this, args);
            handler?.Invoke(this, args);
        }

        private void InternalStart()
        {
            m_isStoppedEvent.Reset();
            IsRunning = true;
            m_monitoringSocket.Connect(Endpoint);
        }

        private void InternalClose()
        {
            try
            {
                m_monitoringSocket.Disconnect(Endpoint);
            }
            catch (Exception)
            {}
            finally
            {
                IsRunning = false;
                m_isStoppedEvent.Set();
            }
        }

        public void AttachToPoller([NotNull] ISocketPollableCollection poller)
        {
            if (poller == null)
                throw new ArgumentNullException(nameof(poller));
            if (IsRunning)
                throw new InvalidOperationException("Monitor already started");
            if (Interlocked.CompareExchange(ref m_attachedPoller, poller, null) != null)
                throw new InvalidOperationException("Already attached to a poller");

            InternalStart();
            poller.Add(m_monitoringSocket);
        }

        public void DetachFromPoller()
        {
            if (m_attachedPoller == null)
                throw new InvalidOperationException("Not attached to a poller");

            m_attachedPoller.Remove(m_monitoringSocket);
            m_attachedPoller = null;
            InternalClose();
        }

        /// <summary>
        /// Start monitor the socket, the method doesn't start a new thread and will block until the monitor poll is stopped
        /// </summary>
        /// <exception cref="InvalidOperationException">The Monitor must not have already started nor attached to a poller.</exception>
        public void Start()
        {
            if (IsRunning)
                throw new InvalidOperationException("Monitor already started");

            if (m_attachedPoller != null)
                throw new InvalidOperationException("Monitor attached to a poller");

            try
            {
                InternalStart();

                while (m_cancel == 0)
                {
                    m_monitoringSocket.Poll(Timeout);
                }
            }
            finally
            {
                InternalClose();
            }
        }

#if !NET35
        /// <summary>
        /// Start a background task for the monitoring operation.
        /// </summary>
        /// <returns></returns>
        public Task StartAsync()
        {
            if (IsRunning)
                throw new InvalidOperationException("Monitor already started");

            if (m_attachedPoller != null)
                throw new InvalidOperationException("Monitor attached to a poller");

            return Task.Factory.StartNew(Start);
        }
#endif

        /// <summary>
        /// Stop monitoring. Blocks until monitoring completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">If this monitor is attached to a poller you must detach it first and not use the stop method.</exception>
        public void Stop()
        {
            if (m_attachedPoller != null)
                throw new InvalidOperationException("Monitor attached to a poller, please detach from poller and don't use the stop method");

            Interlocked.Exchange(ref m_cancel, 1);
            m_isStoppedEvent.WaitOne();
        }

        #region Dispose

        /// <summary>
        /// Release and dispose of any contained resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release and dispose of any contained resources.
        /// </summary>
        /// <param name="disposing">true if releasing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (m_attachedPoller != null)
            {
                DetachFromPoller();
            }
            else if (!m_isStoppedEvent.WaitOne(0))
            {
                Stop();
            }

            m_monitoringSocket.ReceiveReady -= Handle;

#if NET35
            m_isStoppedEvent.Close();
#else
            m_isStoppedEvent.Dispose();
#endif

            if (m_ownsMonitoringSocket)
            {
                m_monitoringSocket.Dispose();
            }
        }

        #endregion
    }
}
