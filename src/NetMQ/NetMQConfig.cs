using System;
using NetMQ.Core;

namespace NetMQ
{
    public static class NetMQConfig
    {
        private static TimeSpan s_linger;

        private static Ctx s_ctx;
        private static int s_socketCount = 0;
        private static object s_sync;
        private static int s_threadPoolSize;
        private static int s_maxSockets;

        static NetMQConfig()
        {
            s_ctx = null;
            s_sync = new object();
            s_linger = TimeSpan.Zero;
            s_threadPoolSize = Ctx.DefaultIOThreads;
            s_maxSockets = Ctx.DefaultMaxSockets;
        }

        static internal SocketBase CreateSocketHandle(ZmqSocketType socketType)
        {
            lock (s_sync)
            {
                if (s_ctx == null)
                {
                    s_ctx = new Ctx();
                    s_ctx.Block = false;
                    s_ctx.IOThreadCount = s_threadPoolSize;
                    s_ctx.MaxSockets = s_maxSockets;
                }

                s_socketCount++;

                return s_ctx.CreateSocket(socketType);
            }
        }

        static internal void SocketDisposed()
        {
            lock (s_sync)
            {
                s_socketCount--;

                if (s_socketCount == 0)
                {
                    s_ctx.Terminate();
                    s_ctx = null;
                }                    
            }
        }

        /// <summary>
        /// Get or set the default linger period for the all sockets,
        /// which determines how long pending messages which have yet to be sent to a peer
        /// shall linger in memory after a socket is closed.
        /// </summary>
        /// <remarks>
        /// This also affects the termination of the socket's context.
        /// -1: Specifies infinite linger period. Pending messages shall not be discarded after the socket is closed;
        /// attempting to terminate the socket's context shall block until all pending messages have been sent to a peer.
        /// 0: The default value of 0 specifies an no linger period. Pending messages shall be discarded immediately when the socket is closed.
        /// Positive values specify an upper bound for the linger period. Pending messages shall not be discarded after the socket is closed;
        /// attempting to terminate the socket's context shall block until either all pending messages have been sent to a peer,
        /// or the linger period expires, after which any pending messages shall be discarded.
        /// </remarks>
        public static TimeSpan Linger
        {
            get
            {
                lock (s_sync)
                {
                    return s_linger;                    
                }
            }
            set
            {
                lock (s_sync)
                {
                    s_linger = value;
                }
            }
        }

        /// <summary>
        /// Get or set the number of IO Threads NetMQ will create, default is 1.
        /// 1 is good for most cases.
        /// </summary>
        public static int ThreadPoolSize
        {
            get
            {
                lock (s_sync)
                {
                    return s_threadPoolSize;
                }
            }
            set
            {
                lock (s_sync)
                {
                    s_threadPoolSize = value;

                    if (s_ctx != null)
                        s_ctx.IOThreadCount = value;
                }
            }
        }

        /// <summary>
        /// Get or set the maximum number of sockets.
        /// </summary>
        public static int MaxSockets
        {
            get
            {
                lock (s_sync)
                {
                    return s_ctx.MaxSockets;
                }
            }
            set
            {
                lock (s_sync)
                {
                    s_maxSockets = value;

                    if (s_ctx != null)
                        s_ctx.MaxSockets = value;
                }
            }
        }      
    }
}
