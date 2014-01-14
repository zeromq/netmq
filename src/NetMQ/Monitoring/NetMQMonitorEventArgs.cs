using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Monitoring
{
    public class NetMQMonitorEventArgs : EventArgs
    {
        public NetMQMonitorEventArgs(NetMQMonitor monitor, string address)
        {
            Monitor = monitor;
            Address = address;
        }

        public NetMQMonitor Monitor { get; private set; }

        public string Address { get; private set; }
    }

    public class NetMQMonitorSocketEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorSocketEventArgs(NetMQMonitor monitor, string address, Socket socket)
            : base(monitor, address)
        {
            Socket = socket;
        }

        public Socket Socket { get; private set; }
    }

    public class NetMQMonitorErrorEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorErrorEventArgs(NetMQMonitor monitor, string address, ErrorCode errorCode)
            : base(monitor, address)
        {
            ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; private set; }
    }

    public class NetMQMonitorIntervalEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorIntervalEventArgs(NetMQMonitor monitor, string address, int interval)
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
