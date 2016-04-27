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
        private static readonly object s_settingsSync;
        private static bool s_manualTakeOver;

        static NetMQConfig()
        {
            s_manualTakeOver = false;
            s_ctx = new Ctx { Block = false };
            s_settingsSync = new object();
            s_linger = TimeSpan.Zero;

            // Register to destroy the context when application exit
            AppDomain.CurrentDomain.ProcessExit += ProcessExitTerminateContext;
        }

        /// <summary>
        /// Handles the context termination.
        /// We use a named method so we can unregister it from the event handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ProcessExitTerminateContext(object sender, EventArgs e)
        {
            try
            {
                s_ctx.CheckDisposed();
                s_ctx.Terminate();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// WARNING:
        /// Please use this with care, this removes the context cleanup, 
        /// and may leave threads running if you don't do proper cleanup.
        /// For proper termination, dispose all sockets, pollers and timmers
        /// and call <see cref="ContextTerminate"/>.
        /// </summary>
        public static void ManualTerminationTakeOver()
        {
            // Ignore subsequence calls
            if (s_manualTakeOver) return;
            s_manualTakeOver = true;
            AppDomain.CurrentDomain.ProcessExit -= ProcessExitTerminateContext;
        }

        /// <summary>
        /// For use in testing
        /// </summary>
        internal static void DisableManualTermination()
        {
            if (!s_manualTakeOver) return;
            s_manualTakeOver = false;
            var isTerminated = false;
            try
            {
                s_ctx.CheckDisposed();
            }
            catch (ObjectDisposedException)
            {
                isTerminated = true;
            }

            // Only creates if we don't have a context.
            if (isTerminated)
                s_ctx = new Ctx { Block = false };
        }

        /// <summary>
        /// WARNING:
        /// This terminate the context, blocking if called with the default options.
        /// This as no effect if ManualTerminationTakeOver isn't called.
        /// </summary>
        /// <param name="block">Should the context block the thread while terminating.</param>
        public static void ContextTerminate(bool block = true)
        {
            // Move along, nothing to see here :)
            if (!s_manualTakeOver) return;

            lock (s_settingsSync)
                s_ctx.Block = block;

            // Gracefully exit if Terminate was already called for the static context.
            try
            {
                s_ctx.CheckDisposed();
                s_ctx.Terminate();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Create a context, if needed.
        /// </summary>
        /// <param name="block">Should the context block the thread while terminating.</param>
        public static void ContextCreate(bool block=false)
        {
            // Move along, nothing to see here :)
            if (!s_manualTakeOver) return;

            var isTerminated = false;
            try
            {
                s_ctx.CheckDisposed();
            }
            catch (ObjectDisposedException)
            {
                isTerminated = true;
            }

            // Only creates if we don't have a context.
            if(isTerminated)
                s_ctx = new Ctx { Block = block };
        }

        internal static Ctx Context => s_ctx;

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
            set { s_ctx.IOThreadCount = value; }
        }

        /// <summary>
        /// Get or set the maximum number of sockets.
        /// </summary>
        public static int MaxSockets
        {
            get { return s_ctx.MaxSockets; }
            set { s_ctx.MaxSockets = value; }
        }      
    }
}
