using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Core;

namespace NetMQ
{
    public static class Global
    {
        private static int s_ioThreadCount = 1;
        private static int s_maxSockets = 1024;

        private static Ctx s_ctx;
        private static object s_sync = new object();
        private static int s_socketCount = 0;

        /// <summary>
        /// Get or set the number of IO Threads in the context, default is 1.
        /// 1 is good for most cases.
        /// </summary>
        public static int ThreadPoolSize
        {
            get
            {
                lock (s_ctx)
                {
                    return s_ioThreadCount;
                }                
            }
            set
            {
                lock (s_ctx)
                {
                    s_ioThreadCount = value;

                    if (s_ctx != null)
                    {
                        s_ctx.IOThreadCount = s_ioThreadCount;
                    }
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
                lock (s_ctx)
                {
                    return s_maxSockets;
                }
            }
            set
            {
                lock (s_ctx)
                {
                    s_maxSockets = value;

                    if (s_ctx != null)
                    {
                        s_ctx.MaxSockets = s_maxSockets;
                    }
                }
            }
        }

        public static bool IsRunning
        {
            get
            {
                lock (s_sync)
                {
                    return s_ctx != null;
                }
            }
        }

        internal static SocketBase CreateSocket(ZmqSocketType socketType)
        {
            lock (s_sync)
            {
                if (s_ctx == null)
                {
                    s_ctx = new Ctx();
                    s_ctx.MaxSockets = s_maxSockets;
                    s_ctx.IOThreadCount = s_ioThreadCount;
                }

                s_socketCount++;

                return s_ctx.CreateSocket(socketType);
            }   
        }

        internal static void ReleaseSocket()
        {
            lock (s_sync)
            {
                s_socketCount--;

                if (s_socketCount == 0)
                {
                    var ctx = s_ctx;
                    s_ctx = null;
                    ctx.Terminate();
                }
            }
        }
    }
}
