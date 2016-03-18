using System;
using NetMQ.Core;

namespace NetMQ
{
    /// <summary>
    /// </summary>
    public static class NetMQConfig
    {
        private static TimeSpan s_linger;

        private static Ctx s_ctx;
        private static object s_settingsSync;

        static NetMQConfig()
        {
            s_ctx = new Ctx();
            s_ctx.Block = false;
            s_settingsSync = new object();
            s_linger = TimeSpan.Zero;

            // Register to destroy the context when application exit
            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainOnProcessExit;
        }

        private static void OnCurrentDomainOnProcessExit(object sender, EventArgs args)
        {
            s_ctx.Terminate();
        }

        internal static Ctx Context
        {
            get
            {
                return s_ctx;                
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
                lock (s_settingsSync)
                {
                    return s_linger;                    
                }
            }
            set
            {
                lock (s_settingsSync)
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
            get { return s_ctx.IOThreadCount; }
            set
            {
                s_ctx.IOThreadCount = value;
            }
        }

        /// <summary>
        /// Get or set the maximum number of sockets.
        /// </summary>
        public static int MaxSockets
        {
            get
            {
                return s_ctx.MaxSockets;
            }
            set { s_ctx.MaxSockets = value; }
        }      
    }
}
