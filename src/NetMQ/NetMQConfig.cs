using System;
using JetBrains.Annotations;
using NetMQ.Core;

namespace NetMQ
{
    public static class NetMQConfig
    {
        private static TimeSpan s_linger;

        [CanBeNull] private static Ctx s_ctx;
        private static int s_threadPoolSize = Ctx.DefaultIOThreads;
        private static int s_maxSockets = Ctx.DefaultMaxSockets;
        private static readonly object s_sync;

        static NetMQConfig()
        {
            s_sync = new object();
            s_linger = TimeSpan.Zero;
        }

        internal static Ctx Context
        {
            get
            {
                // Optimise for the case where the value is non-null, and we don't need to acquire the lock
                var c = s_ctx;
                if (c != null)
                    return c;

                lock (s_sync)
                {
                    // Check again whether it's null now that we have the lock
                    if (s_ctx == null)
                    {
                        s_ctx = new Ctx();
                        s_ctx.IOThreadCount = s_threadPoolSize;
                        s_ctx.MaxSockets = s_maxSockets;
                    }

                    return s_ctx;
                }
            }
        }

        /// <summary>
        /// Cleanup library resources, call this method when your process is shutting-down.
        /// </summary>
        /// <param name="block">Set to true when you want to make sure sockets send all pending messages</param>
        public static void Cleanup(bool block = true)
        {
            lock (s_sync)
            {
                if (s_ctx != null)
                {
                    s_ctx.Terminate(block);
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
        /// <para />
        /// -1: Specifies infinite linger period. Pending messages shall not be discarded after the socket is closed;
        /// attempting to terminate the socket's context shall block until all pending messages have been sent to a peer.
        /// <para />
        /// 0: The default value of <see cref="TimeSpan.Zero"/> specifies no linger period. Pending messages shall be discarded immediately when the socket is closed.
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
                    return s_threadPoolSize;
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
                    return s_maxSockets;
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

        #region Obsolete

        /// <summary>
        /// Method is obsolete, call Cleanup instead
        /// </summary>
        [Obsolete("Use Cleanup method")]
        public static void ManualTerminationTakeOver()
        {
        }

        /// <summary>
        /// Method is obsolete, call Cleanup instead
        /// </summary>
        [Obsolete("Use Cleanup method")]
        internal static void DisableManualTermination()
        {
        }

        /// <summary>
        /// Method is obsolete, call Cleanup instead
        /// </summary>
        /// <param name="block">Should the context block the thread while terminating.</param>
        [Obsolete("Use Cleanup method")]
        public static void ContextTerminate(bool block = true)
        {
        }

        /// <summary>
        /// Method is obsolete, context created automatically
        /// </summary>
        [Obsolete("Context is created automatically")]
        public static void ContextCreate(bool block = false)
        {
            Cleanup(block);
        }

        #endregion
    }
}
