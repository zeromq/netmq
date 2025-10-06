using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;
using NetMQ.Core;
using NetMQ.Sockets;

namespace NetMQ.Monitoring
{
    /// <inheritdoc />
    public class NetMQMonitor : INetMQMonitor
    {
        private readonly NetMQSocket m_monitoringSocket;
        private readonly bool m_ownsMonitoringSocket;
        private INetMQPoller? m_attachedPoller;
        private int m_cancel;

        private readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(true);

        /// <summary>
        /// Create a new monitor object
        /// </summary>
        /// <param name="monitoredSocket">Socket to monitor</param>
        /// <param name="endpoint">Bind endpoint</param>
        /// <param name="eventsToMonitor">Flag enum of the events to monitored</param>
        public NetMQMonitor(NetMQSocket monitoredSocket, string endpoint, SocketEvents eventsToMonitor)
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
        public NetMQMonitor(NetMQSocket socket, string endpoint, bool ownsSocket = false)
        {
            Endpoint = endpoint;
            Timeout = TimeSpan.FromSeconds(0.5);
            m_monitoringSocket = socket;
            m_monitoringSocket.ReceiveReady += Handle;

            m_ownsMonitoringSocket = ownsSocket;
        }

        /// <inheritdoc />
        public string Endpoint { get; }

        /// <inheritdoc />
        public bool IsRunning { get; private set; }

        /// <inheritdoc />
        public TimeSpan Timeout { get; set; }

        #region Events

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorEventArgs>? EventReceived;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorSocketEventArgs>? Connected;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorErrorEventArgs>? ConnectDelayed;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorIntervalEventArgs>? ConnectRetried;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorSocketEventArgs>? Listening;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorErrorEventArgs>? BindFailed;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorSocketEventArgs>? Accepted;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorErrorEventArgs>? AcceptFailed;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorSocketEventArgs>? Closed;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorErrorEventArgs>? CloseFailed;

        /// <inheritdoc />
        public event EventHandler<NetMQMonitorSocketEventArgs>? Disconnected;

        #endregion

        private void Handle(object? sender, NetMQSocketEventArgs socketEventArgs)
        {
            var monitorEvent = MonitorEvent.Read(m_monitoringSocket.SocketHandle);

            switch (monitorEvent.Event)
            {
                case SocketEvents.Connected:
                    InvokeEvent(Connected, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, monitorEvent.ConvertArg<AsyncSocket>(), SocketEvents.Connected));
                    break;
                case SocketEvents.ConnectDelayed:
                    InvokeEvent(ConnectDelayed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.ConvertArg<int>(), SocketEvents.ConnectDelayed));
                    break;
                case SocketEvents.ConnectRetried:
                    InvokeEvent(ConnectRetried, new NetMQMonitorIntervalEventArgs(this, monitorEvent.Addr, monitorEvent.ConvertArg<int>(), SocketEvents.ConnectRetried));
                    break;
                case SocketEvents.Listening:
                    InvokeEvent(Listening, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, monitorEvent.ConvertArg<AsyncSocket>(), SocketEvents.Listening));
                    break;
                case SocketEvents.BindFailed:
                    InvokeEvent(BindFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.ConvertArg<int>(), SocketEvents.BindFailed));
                    break;
                case SocketEvents.Accepted:
                    InvokeEvent(Accepted, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, monitorEvent.ConvertArg<AsyncSocket>(), SocketEvents.Accepted));
                    break;
                case SocketEvents.AcceptFailed:
                    InvokeEvent(AcceptFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.ConvertArg<int>(), SocketEvents.AcceptFailed));
                    break;
                case SocketEvents.Closed:
                    InvokeEvent(Closed, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, monitorEvent.ConvertArg<AsyncSocket>(), SocketEvents.Closed));
                    break;
                case SocketEvents.CloseFailed:
                    InvokeEvent(CloseFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.ConvertArg<int>(), SocketEvents.CloseFailed));
                    break;
                case SocketEvents.Disconnected:
                    InvokeEvent(Disconnected, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, monitorEvent.ConvertArg<AsyncSocket>(), SocketEvents.Disconnected));
                    break;
                default:
                    throw new Exception("unknown event " + monitorEvent.Event);
            }
        }

        private void InvokeEvent<T>(EventHandler<T>? handler, T args) where T : NetMQMonitorEventArgs
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

        /// <inheritdoc />
        public void AttachToPoller<T>(T poller) where T : INetMQPoller
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

        /// <inheritdoc />
        public void DetachFromPoller()
        {
            DetachFromPoller(false);
        }
        
        private void DetachFromPoller(bool dispose)
        {
            if (m_attachedPoller == null)
                throw new InvalidOperationException("Not attached to a poller");
            
            if (dispose)
                m_attachedPoller.RemoveAndDispose(m_monitoringSocket);
            else
                m_attachedPoller.Remove(m_monitoringSocket);
            m_attachedPoller = null;
            InternalClose();
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public Task StartAsync()
        {
            if (IsRunning)
                throw new InvalidOperationException("Monitor already started");

            if (m_attachedPoller != null)
                throw new InvalidOperationException("Monitor attached to a poller");

            return Task.Factory.StartNew(Start);
        }

        /// <inheritdoc />
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

            bool attachedToPoller = m_attachedPoller != null; 

            if (attachedToPoller)
            {
                DetachFromPoller(m_ownsMonitoringSocket);
            }
            else if (!m_isStoppedEvent.WaitOne(0))
            {
                Stop();
            }

            m_monitoringSocket.ReceiveReady -= Handle;

            m_isStoppedEvent.Dispose();

            if (m_ownsMonitoringSocket && !attachedToPoller)
            {
                m_monitoringSocket.Dispose();
            }
        }

        #endregion
    }
}
