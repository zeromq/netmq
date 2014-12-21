using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using AsyncIO;
using NetMQ.zmq;

namespace NetMQ.Monitoring
{
    public class NetMQMonitorEventArgs : EventArgs
    {
        public NetMQMonitorEventArgs(INetMQMonitor monitor, string address)
        {
            Monitor = monitor;
            Address = address;
        }

		public INetMQMonitor Monitor { get; private set; }

        public string Address { get; private set; }
    }

    public class NetMQMonitorSocketEventArgs : NetMQMonitorEventArgs
    {
		public NetMQMonitorSocketEventArgs(INetMQMonitor monitor, string address, AsyncSocket socket)
            : base(monitor, address)
        {
            Socket = socket;
        }

        public AsyncSocket Socket { get; private set; }
    }

    public class NetMQMonitorErrorEventArgs : NetMQMonitorEventArgs
    {
		public NetMQMonitorErrorEventArgs(INetMQMonitor monitor, string address, ErrorCode errorCode)
            : base(monitor, address)
        {
            ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; private set; }
    }

    public class NetMQMonitorIntervalEventArgs : NetMQMonitorEventArgs
    {
		public NetMQMonitorIntervalEventArgs(INetMQMonitor monitor, string address, int interval)
            : base(monitor, address)
        {
            Interval = interval;
        }

        /// <summary>
        /// Gets the computed reconnect interval.
        /// </summary>
        public int Interval { get; private set; }
    }

}
