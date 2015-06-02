using System;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Monitoring
{
    /// <summary>
    /// This is an EventArgs that also contains a NetMQMonitor and a string Address.
    /// </summary>
    public abstract class NetMQMonitorEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorEventArgs that contains the given monitor and address.
        /// </summary>
        /// <param name="monitor">The <see cref="NetMQMonitor"/> that raised this event.</param>
        /// <param name="address">The address of the event.</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        protected NetMQMonitorEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, SocketEvent socketEvent)
        {
            Monitor = monitor;
            Address = address;
            SocketEvent = socketEvent;
        }

        /// <summary>
        /// Gets the <see cref="NetMQMonitor"/> that raised this event.
        /// </summary>
        [NotNull]
        public NetMQMonitor Monitor { get; private set; }

        /// <summary>
        /// Gets the address of the event.
        /// </summary>
        [NotNull]
        public string Address { get; private set; }

        /// <summary>
        /// Gets the type of socket event that occurred.
        /// </summary>
        public SocketEvent SocketEvent { get; private set; }
    }

    /// <summary>
    /// A NetMQMonitorSocketEventArgs is a subclass of NetMQMonitorEventArgs that also holds a socket.
    /// </summary>
    public class NetMQMonitorSocketEventArgs : NetMQMonitorEventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorSocketEventArgs that contains the given monitor, address, and socket.
        /// </summary>
        /// <param name="monitor">the NetMQMonitor that this event concerns</param>
        /// <param name="address">a string denoting the endpoint-address</param>
        /// <param name="socket">the AsyncSocket in question</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        public NetMQMonitorSocketEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, [NotNull] AsyncSocket socket, SocketEvent socketEvent)
            : base(monitor, address, socketEvent)
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
        /// <summary>
        /// Create a new NetMQMonitorErrorEventArgs that contains the given monitor, address, and error-code.
        /// </summary>
        /// <param name="monitor">the NetMQMonitor that this event concerns</param>
        /// <param name="address">a string denoting the endpoint-address</param>
        /// <param name="errorCode">the ErrorCode that is giving rise to this event</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        public NetMQMonitorErrorEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, ErrorCode errorCode, SocketEvent socketEvent)
            : base(monitor, address, socketEvent)
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
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        public NetMQMonitorIntervalEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, int interval, SocketEvent socketEvent)
            : base(monitor, address, socketEvent)
        {
            Interval = interval;
        }

        /// <summary>
        /// Gets the computed reconnect interval.
        /// </summary>
        public int Interval { get; private set; }
    }
}
