using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Core;
using NetMQ.Sockets;
using NetMQ.Monitoring;

namespace NetMQ
{
    /// <summary>
    /// Context class of the NetMQ, you should have only one context in your application
    /// </summary>
    public class NetMQContext : IDisposable
    {
        readonly Ctx m_ctx;
        private int m_isClosed = 0;


        private NetMQContext(Ctx ctx)
        {
            m_ctx = ctx;
        }

        /// <summary>
        /// Create a new context
        /// </summary>
        /// <returns>The new context</returns>
        public static NetMQContext Create()
        {
            return new NetMQContext(new Ctx());
        }

        /// <summary>
        /// Number of IO Threads in the context, default is 1, 1 is good for most cases
        /// </summary>
        public int ThreadPoolSize
        {
            get { return m_ctx.Get(ContextOption.IOThreads); }
            set { m_ctx.Set(ContextOption.IOThreads, value); }
        }

        /// <summary>
        /// Maximum number of sockets
        /// </summary>
        public int MaxSockets
        {
            get { return m_ctx.Get(ContextOption.MaxSockets); }
            set { m_ctx.Set(ContextOption.MaxSockets, value); }
        }

        [Obsolete("Use Create specific socket instead")]
        public NetMQSocket CreateSocket(ZmqSocketType socketType)
        {
            var socketHandle = m_ctx.CreateSocket(socketType);

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
        /// Create request socket
        /// </summary>
        /// <returns></returns>
        public RequestSocket CreateRequestSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Req);

            return new RequestSocket(socketHandle);
        }

        /// <summary>
        /// Create response socket
        /// </summary>
        /// <returns></returns>
        public ResponseSocket CreateResponseSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Rep);

            return new ResponseSocket(socketHandle);
        }

        /// <summary>
        /// Create dealer socket
        /// </summary>
        /// <returns></returns>
        public DealerSocket CreateDealerSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Dealer);

            return new DealerSocket(socketHandle);
        }

        /// <summary>
        /// Create router socket
        /// </summary>
        /// <returns></returns>
        public RouterSocket CreateRouterSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Router);

            return new RouterSocket(socketHandle);
        }

        /// <summary>
        /// Create xpublisher socket
        /// </summary>
        /// <returns></returns>
        public XPublisherSocket CreateXPublisherSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Xpub);

            return new XPublisherSocket(socketHandle);
        }

        /// <summary>
        /// Create pair socket
        /// </summary>
        /// <returns></returns>
        public PairSocket CreatePairSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Pair);

            return new PairSocket(socketHandle);
        }

        /// <summary>
        /// Create push socket
        /// </summary>
        /// <returns></returns>
        public PushSocket CreatePushSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Push);

            return new PushSocket(socketHandle);
        }

        /// <summary>
        /// Create publisher socket
        /// </summary>
        /// <returns></returns>
        public PublisherSocket CreatePublisherSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Pub);

            return new PublisherSocket(socketHandle);
        }

        /// <summary>
        /// Create pull socket
        /// </summary>
        /// <returns></returns>
        public PullSocket CreatePullSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Pull);

            return new PullSocket(socketHandle);
        }

        /// <summary>
        /// Create subscriber socket
        /// </summary>
        /// <returns></returns>
        public SubscriberSocket CreateSubscriberSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Sub);

            return new SubscriberSocket(socketHandle);
        }

        /// <summary>
        /// Create xsub socket
        /// </summary>
        /// <returns></returns>
        public XSubscriberSocket CreateXSubscriberSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Xsub);

            return new XSubscriberSocket(socketHandle);
        }

        public StreamSocket CreateStreamSocket()
        {
            var socketHandle = m_ctx.CreateSocket(ZmqSocketType.Stream);

            return new StreamSocket(socketHandle);
        }

        public NetMQMonitor CreateMonitorSocket(string endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to monitor to an empty endpoint.", "endpoint");
            }

            return new NetMQMonitor(CreatePairSocket(), endpoint);
        }

        /// <summary>
        /// Close the context
        /// </summary>
        public void Terminate()
        {
            if (Interlocked.CompareExchange(ref m_isClosed, 1, 0) == 0)
            {
                m_ctx.Terminate();                
            }
        }

        public void Dispose()
        {
            Terminate();
        }
    }
}
