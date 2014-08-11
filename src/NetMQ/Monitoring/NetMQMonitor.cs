using System;
using System.Net.Sockets;
using System.Threading;
using NetMQ.zmq;

namespace NetMQ.Monitoring
{
    /// <summary>
    /// Use the  class when you want to monitor socket
    /// </summary>
    public class NetMQMonitor : IDisposable
    {
        private bool m_isOwner;
        private Poller m_attachedPoller = null;

        readonly CancellationTokenSource m_cancellationTokenSource = new CancellationTokenSource();

        private readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(true);

        public NetMQMonitor(NetMQContext context, NetMQSocket monitoredSocket, string endpoint, SocketEvent eventsToMonitor)
        {
            Endpoint = endpoint;
            Timeout = TimeSpan.FromSeconds(0.5);

            ZMQ.SocketMonitor(monitoredSocket.SocketHandle, Endpoint, eventsToMonitor);

            MonitoringSocket = context.CreatePairSocket();
            MonitoringSocket.Options.Linger = TimeSpan.Zero;

            MonitoringSocket.ReceiveReady += Handle;

            m_isOwner = true;
        }

        /// <summary>
        /// This constuctor received already created monitored socket. other constructor is preferred, this one is here to support clrzmq singature
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="endpoint"></param>
        public NetMQMonitor(NetMQSocket socket, string endpoint)
        {
            Endpoint = endpoint;
            Timeout = TimeSpan.FromSeconds(0.5);
            MonitoringSocket = socket;

            MonitoringSocket.ReceiveReady += Handle;

            m_isOwner = false;
        }

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
        /// Occurs when the stream engine (tcp and ipc specific) detects a corrupted / broken session.
        /// </summary>
        public event EventHandler<NetMQMonitorSocketEventArgs> Disconnected;

        /// <summary>
        /// The monitoring address
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// Monitoring socket created by the init method
        /// </summary>
        internal NetMQSocket MonitoringSocket { get; private set; }

        public bool IsRunning { get; private set; }

        /// <summary>
        /// How much time to wait on each poll iteration, the higher the number the longer it will take the poller to stop 
        /// </summary>
        public TimeSpan Timeout { get; set; }

        internal void Handle(object sender, NetMQSocketEventArgs socketEventArgs)
        {
            MonitorEvent monitorEvent = MonitorEvent.Read(MonitoringSocket.SocketHandle);

            if (monitorEvent != null)
            {
                switch (monitorEvent.Event)
                {
                    case SocketEvent.Connected:
                        InvokeEvent(Connected, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (Socket)monitorEvent.Arg));
                        break;
                    case SocketEvent.ConnectDelayed:
                        InvokeEvent(ConnectDelayed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg));
                        break;
                    case SocketEvent.ConnectRetried:
                        InvokeEvent(ConnectRetried, new NetMQMonitorIntervalEventArgs(this, monitorEvent.Addr, (int)monitorEvent.Arg));
                        break;
                    case SocketEvent.Listening:
                        InvokeEvent(Listening, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (Socket)monitorEvent.Arg));
                        break;
                    case SocketEvent.BindFailed:
                        InvokeEvent(BindFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg));
                        break;
                    case SocketEvent.Accepted:
                        InvokeEvent(Accepted, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (Socket)monitorEvent.Arg));
                        break;
                    case SocketEvent.AcceptFailed:
                        InvokeEvent(AcceptFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg));
                        break;
                    case SocketEvent.Closed:
                        InvokeEvent(Closed, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (Socket)monitorEvent.Arg));
                        break;
                    case SocketEvent.CloseFailed:
                        InvokeEvent(CloseFailed, new NetMQMonitorErrorEventArgs(this, monitorEvent.Addr, (ErrorCode)monitorEvent.Arg));
                        break;
                    case SocketEvent.Disconnected:
                        InvokeEvent(Disconnected, new NetMQMonitorSocketEventArgs(this, monitorEvent.Addr, (Socket)monitorEvent.Arg));
                        break;
                    default:
                        throw new Exception("unknown event " + monitorEvent.Event.ToString());
                }
            }
        }

        private void InvokeEvent<T>(EventHandler<T> handler, T args) where T : NetMQMonitorEventArgs
        {
            var temp = handler;
            if (temp != null)
            {
                temp(this, args);
            }
        }

        private void InternalStart()
        {
            m_isStoppedEvent.Reset();
            IsRunning = true;
            MonitoringSocket.Connect(Endpoint);
        }

        private void InternalClose()
        {
            m_isStoppedEvent.Set();
            IsRunning = false;
            MonitoringSocket.Disconnect(Endpoint);
        }

        public void AttachToPoller(Poller poller)
        {
            InternalStart();
            m_attachedPoller = poller;
            poller.AddSocket(MonitoringSocket);
        }

        public void DetachFromPoller()
        {
            m_attachedPoller.RemoveSocket(MonitoringSocket);
            m_attachedPoller = null;
            InternalClose();
        }

        /// <summary>
        /// Start monitor the socket, the method doesn't start a new thread and will block until the monitor poll is stopped
        /// </summary>
        public void Start()
        {
            // in case the sockets is created in another thread
            Thread.MemoryBarrier();

            if (IsRunning)
            {
                throw new InvalidOperationException("Monitor already started");
            }

            if (m_attachedPoller != null)
            {
                throw new InvalidOperationException("Monitor attached to a poller");
            }

            InternalStart();

            try
            {
                while (!m_cancellationTokenSource.IsCancellationRequested)
                {
                    MonitoringSocket.Poll(Timeout);
                }
            }
            finally
            {
                InternalClose();
            }
        }

        // Stop the socket monitoring
        public void Stop()
        {
            if (m_attachedPoller != null)
            {
                throw new InvalidOperationException("Monitor attached to a poller, please detach from poller and don't use the stop method");
            }

            m_cancellationTokenSource.Cancel();
            m_isStoppedEvent.WaitOne();
        }

        public void Dispose()
        {
            if (m_attachedPoller != null)
            {
                DetachFromPoller();
            }
            else if (!m_isStoppedEvent.WaitOne(0))
            {
                Stop();
            }

            if (m_isOwner)
            {
                MonitoringSocket.Dispose();
            }
        }
    }
}
