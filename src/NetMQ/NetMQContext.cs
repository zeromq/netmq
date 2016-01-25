using System;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Core;
using NetMQ.Monitoring;
using NetMQ.Sockets;

namespace NetMQ
{
    /// <summary>
    /// NetMQContext is an old concept and is no longer needed. You can create sockets with new keyword, for example new RequestSocket();
    /// Please note that when creating socket with new keyword the default value for SocketOptions.Linger is zero and not indefinitly like with context.
    /// NetMQContext is the context class of the NetMQ message-queueing subsystem.
    /// This contains all of the state-information for the message-queueing subsystem, and provides
    /// methods for socket creation.
    /// You should (ordinarily) have only one context in your application process.
    /// </summary>
    /// <remarks>
    /// The NetMQContext is used to create all sockets. Thus, to use NetMQ you should start by calling <c>NetMQContext.Create()</c>
    /// to create an instance. NetMQContext is an <c>IDisposable</c> so you can use it within a using block:
    /// <code>
    /// using (var context = NetMQContext.Create())
    /// {
    ///     // Put your code in here. Exit this block to dispose the context
    ///     // only when communication is no longer required.
    /// }
    /// </code>
    /// You should create and use exactly one context in your process.
    /// </remarks>   
    [Obsolete("NetMQContext is obsolete, you can create sockets with new keyword, like new DealerSocket")]
    public class NetMQContext : IDisposable
    {
        private readonly Ctx m_ctx;
        private int m_isClosed;

        private NetMQContext([NotNull] Ctx ctx)
        {
            m_ctx = ctx;
        }

        /// <summary>
        /// Create and return a new context.
        /// </summary>
        /// <returns>the new NetMQContext</returns>
        [NotNull]
        public static NetMQContext Create()
        {
            return new NetMQContext(new Ctx());
        }

        /// <summary>
        /// Get or set the number of IO Threads in the context, default is 1.
        /// 1 is good for most cases.
        /// </summary>
        public int ThreadPoolSize
        {
            get { m_ctx.CheckDisposed(); return m_ctx.IOThreadCount; }
            set { m_ctx.CheckDisposed(); m_ctx.IOThreadCount = value; }
        }

        /// <summary>
        /// Get or set the maximum number of sockets.
        /// </summary>
        public int MaxSockets
        {
            get { m_ctx.CheckDisposed(); return m_ctx.MaxSockets; }
            set { m_ctx.CheckDisposed(); m_ctx.MaxSockets = value; }
        }

        /// <summary>
        /// Should context wait for all sockets before terminating?
        /// </summary>
        public bool Block
        {
            get
            {
                m_ctx.CheckDisposed();
                return m_ctx.Block;
            }
            set
            {
                m_ctx.CheckDisposed();
                m_ctx.Block = value;
            }
        }

        #region Socket Creation

        /// <summary>
        /// Create a socket of the given type within this context.
        /// </summary>
        /// <param name="socketType">a ZmqSocketType denoting which type of socket to create</param>
        /// <returns>a new socket of the given type</returns>
        [CanBeNull]
        private SocketBase CreateHandle(ZmqSocketType socketType)
        {
            m_ctx.CheckDisposed();

            return m_ctx.CreateSocket(socketType);
        }

        /// <summary>
        /// Create and return a new socket of the given socketType.
        /// </summary>
        /// <param name="socketType">a ZmqSocketType indicating the type of socket to create</param>
        /// <returns>a new socket - a subclass of NetMQSocket</returns>
        /// <exception cref="ArgumentOutOfRangeException">The socketType must be a valid value.</exception>
        [NotNull]
        public NetMQSocket CreateSocket(ZmqSocketType socketType)
        {
            var socketHandle = CreateHandle(socketType);

            switch (socketType)
            {
                case ZmqSocketType.Pair:
                    return new PairSocket(socketHandle);
                case ZmqSocketType.Pub:
                    return new PublisherSocket(socketHandle);
                case ZmqSocketType.Sub:
                    return new SubscriberSocket(socketHandle);
                case ZmqSocketType.Req:
                    return new RequestSocket(socketHandle);
                case ZmqSocketType.Rep:
                    return new ResponseSocket(socketHandle);
                case ZmqSocketType.Dealer:
                    return new DealerSocket(socketHandle);
                case ZmqSocketType.Router:
                    return new RouterSocket(socketHandle);
                case ZmqSocketType.Pull:
                    return new PullSocket(socketHandle);
                case ZmqSocketType.Push:
                    return new PushSocket(socketHandle);
                case ZmqSocketType.Xpub:
                    return new XPublisherSocket(socketHandle);
                case ZmqSocketType.Xsub:
                    return new XSubscriberSocket(socketHandle);
                case ZmqSocketType.Stream:
                    return new StreamSocket(socketHandle);
                default:
                    throw new ArgumentOutOfRangeException("socketType");
            }
        }

        /// <summary>
        /// Create and return a new request-socket.
        /// </summary>
        /// <returns>the new RequestSocket</returns>
        [NotNull]
        public RequestSocket CreateRequestSocket()
        {
            return new RequestSocket(CreateHandle(ZmqSocketType.Req));
        }

        /// <summary>
        /// Create and return a new response-socket.
        /// </summary>
        /// <returns>the new ResponseSocket</returns>
        [NotNull]
        public ResponseSocket CreateResponseSocket()
        {
            return new ResponseSocket(CreateHandle(ZmqSocketType.Rep));
        }

        /// <summary>
        /// Create and return a new dealer-socket.
        /// </summary>
        /// <returns>the new DealerSocket</returns>
        [NotNull]
        public DealerSocket CreateDealerSocket()
        {
            return new DealerSocket(CreateHandle(ZmqSocketType.Dealer));
        }

        /// <summary>
        /// Create and return a new router-socket.
        /// </summary>
        /// <returns>the new RouterSocket</returns>
        [NotNull]
        public RouterSocket CreateRouterSocket()
        {
            return new RouterSocket(CreateHandle(ZmqSocketType.Router));
        }

        /// <summary>
        /// Create and return a new xpublisher-socket.
        /// </summary>
        /// <returns>the new XPublisherSocket</returns>
        [NotNull]
        public XPublisherSocket CreateXPublisherSocket()
        {
            return new XPublisherSocket(CreateHandle(ZmqSocketType.Xpub));
        }

        /// <summary>
        /// Create and return a new pair-socket.
        /// </summary>
        /// <returns>the new PairSocket</returns>
        [NotNull]
        public PairSocket CreatePairSocket()
        {
            return new PairSocket(CreateHandle(ZmqSocketType.Pair));
        }

        /// <summary>
        /// Create and return a new push-socket.
        /// </summary>
        /// <returns>the new PushSocket</returns>
        [NotNull]
        public PushSocket CreatePushSocket()
        {
            return new PushSocket(CreateHandle(ZmqSocketType.Push));
        }

        /// <summary>
        /// Create and return a new publisher-socket.
        /// </summary>
        /// <returns>the new PublisherSocket</returns>
        [NotNull]
        public PublisherSocket CreatePublisherSocket()
        {
            return new PublisherSocket(CreateHandle(ZmqSocketType.Pub));
        }

        /// <summary>
        /// Create and return a new pull-socket.
        /// </summary>
        /// <returns>the new PullSocket</returns>
        [NotNull]
        public PullSocket CreatePullSocket()
        {
            return new PullSocket(CreateHandle(ZmqSocketType.Pull));
        }

        /// <summary>
        /// Create and return a new subscriber-socket.
        /// </summary>
        /// <returns>the new SubscriberSocket</returns>
        [NotNull]
        public SubscriberSocket CreateSubscriberSocket()
        {
            return new SubscriberSocket(CreateHandle(ZmqSocketType.Sub));
        }

        /// <summary>
        /// Create and return a new xsubscriber-socket.
        /// </summary>
        /// <returns>the new XSubscriberSocket</returns>
        [NotNull]
        public XSubscriberSocket CreateXSubscriberSocket()
        {
            return new XSubscriberSocket(CreateHandle(ZmqSocketType.Xsub));
        }

        /// <summary>
        /// Create and return a new stream-socket.
        /// </summary>
        /// <returns>the new StreamSocket</returns>
        [NotNull]
        public StreamSocket CreateStreamSocket()
        {
            return new StreamSocket(CreateHandle(ZmqSocketType.Stream));
        }

        /// <summary>
        /// Create and return a new monitor-socket that monitors the given endpoint.
        /// </summary>
        /// <param name="endpoint">a string denoting the endpoint to be monitored</param>
        /// <returns>the new NetMQMonitor</returns>
        /// <exception cref="ArgumentNullException">endpoint must not be null.</exception>
        /// <exception cref="ArgumentException">endpoint must not be an empty string.</exception>
        [NotNull]
        public NetMQMonitor CreateMonitorSocket([NotNull] string endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to monitor to an empty endpoint.", "endpoint");
            }

            return new NetMQMonitor(CreatePairSocket(), endpoint, ownsSocket: true);
        }

        #endregion

        /// <summary>
        /// Close (terminate) this context.
        /// </summary>
        /// <remarks>
        /// May be safely called even if already terminated.
        /// This must not be called on a context that is already closed otherwise an ObjectDisposedException is thrown.
        /// </remarks>
        public void Terminate()
        {
            if (Interlocked.CompareExchange(ref m_isClosed, 1, 0) != 0)
                return;

            m_ctx.CheckDisposed();
            m_ctx.Terminate();
        }

        /// <summary>
        /// Close (or terminate) this context.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Close (or terminate) this context.
        /// </summary>
        /// <param name="disposing">true if releasing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Terminate();
        }
    }
}
