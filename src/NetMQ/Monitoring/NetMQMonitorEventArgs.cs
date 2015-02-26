using System;
using AsyncIO;

namespace NetMQ.Monitoring
{
    /// <summary>
    /// This is an EventArgs that also contains a NetMQMonitor and a string Address.
    /// </summary>
    public class NetMQMonitorEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorEventArgs that contains the given monitor and address.
        /// </summary>
        /// <param name="monitor">a NetMQMonitor for this to hold</param>
        /// <param name="address">a string address for this to hold</param>
        public NetMQMonitorEventArgs(NetMQMonitor monitor, string address)
        {
            Monitor = monitor;
            Address = address;
        }

        /// <summary>
        /// Get the NetMQMonitor that this NetMQMonitorEventArgs is holding.
        /// </summary>
        public NetMQMonitor Monitor { get; private set; }

        /// <summary>
        /// Get the address, as a string, that this NetMQMonitorEventArgs is holding.
        /// </summary>
        public string Address { get; private set; }
    }

    /// <summary>
    /// A NetMQMonitorSocketEventArgs is a subclass of NetMQMonitorEventArgs that also holds a socket.
    /// </summary>
    public class NetMQMonitorSocketEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorSocketEventArgs(NetMQMonitor monitor, string address, AsyncSocket socket)
            : base(monitor, address)
        {
            Socket = socket;
        }

        /// <summary>
        /// Get the AsyncSocket that this is holding.
        /// </summary>
        public AsyncSocket Socket { get; private set; }
    }

    /// <summary>
    /// A NetMQMonitorErrorEventArgs is a subclass of NetMQMonitorEventArgs that also holds an ErrorCode.
    /// </summary>
    public class NetMQMonitorErrorEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorErrorEventArgs(NetMQMonitor monitor, string address, ErrorCode errorCode)
            : base(monitor, address)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Get the ErrorCode that this was constructed with.
        /// </summary>
        public ErrorCode ErrorCode { get; private set; }
    }

    /// <summary>
    /// A NetMQMonitorIntervalEventArgs is a subclass of NetMQMonitorEventArgs that also provides an Interval property to hold the reconnect-interval.
    /// </summary>
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
