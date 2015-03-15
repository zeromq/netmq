using System;
using AsyncIO;
using JetBrains.Annotations;

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
        public NetMQMonitorEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address)
        {
            Monitor = monitor;
            Address = address;
        }

        /// <summary>
        /// Get the NetMQMonitor that this NetMQMonitorEventArgs is holding.
        /// </summary>
        [NotNull]
        public NetMQMonitor Monitor { get; private set; }

        /// <summary>
        /// Get the address, as a string, that this NetMQMonitorEventArgs is holding.
        /// </summary>
        [NotNull]
        public string Address { get; private set; }
    }

    /// <summary>
    /// A NetMQMonitorSocketEventArgs is a subclass of NetMQMonitorEventArgs that also holds a socket.
    /// </summary>
    public class NetMQMonitorSocketEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorSocketEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, [NotNull] AsyncSocket socket)
            : base(monitor, address)
        {
            Socket = socket;
        }

        /// <summary>
        /// Get the AsyncSocket that this is holding.
        /// </summary>
        [NotNull]
        public AsyncSocket Socket { get; private set; }
    }

    /// <summary>
    /// A NetMQMonitorErrorEventArgs is a subclass of NetMQMonitorEventArgs that also holds an ErrorCode.
    /// </summary>
    public class NetMQMonitorErrorEventArgs : NetMQMonitorEventArgs
    {
        public NetMQMonitorErrorEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, ErrorCode errorCode)
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
        /// <summary>
        /// Create a new NetMQMonitorIntervalEventArgs containing the given NetMQMonitor, address, and time-interval.
        /// </summary>
        /// <param name="monitor">the NetMQMonitor</param>
        /// <param name="address">a string denoting the address</param>
        /// <param name="interval">the computed reconnect-interval</param>
        public NetMQMonitorIntervalEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, int interval)
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
