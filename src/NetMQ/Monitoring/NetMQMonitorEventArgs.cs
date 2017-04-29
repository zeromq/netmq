using System;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Monitoring
{
    /// <summary>
    /// Base class for all event arguments raised by <see cref="NetMQMonitor"/>.
    /// </summary>
    public abstract class NetMQMonitorEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorEventArgs that contains the given monitor and address.
        /// </summary>
        /// <param name="monitor">The <see cref="NetMQMonitor"/> that raised this event.</param>
        /// <param name="address">The address of the event.</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        protected NetMQMonitorEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, SocketEvents socketEvent)
        {
            Monitor = monitor;
            Address = address;
            SocketEvent = socketEvent;
        }

        /// <summary>
        /// Gets the <see cref="NetMQMonitor"/> that raised this event.
        /// </summary>
        [NotNull]
        public NetMQMonitor Monitor { get; }

        /// <summary>
        /// Gets the address of the event.
        /// </summary>
        [NotNull]
        public string Address { get; }

        /// <summary>
        /// Gets the type of socket event that occurred.
        /// </summary>
        public SocketEvents SocketEvent { get; }
    }

    /// <summary>
    /// A subclass of <see cref="NetMQMonitorEventArgs"/> that also holds a socket.
    /// </summary>
    public class NetMQMonitorSocketEventArgs : NetMQMonitorEventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorSocketEventArgs that contains the given monitor, address, and socket.
        /// </summary>
        /// <param name="monitor">The <see cref="NetMQMonitor"/> that raised this event.</param>
        /// <param name="address">The address of the event.</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        /// <param name="socket">The socket upon which this event occurred.</param>
        public NetMQMonitorSocketEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, [NotNull] AsyncSocket socket, SocketEvents socketEvent)
            : base(monitor, address, socketEvent)
        {
            Socket = socket;
        }

        /// <summary>
        /// Gets the socket upon which this event occurred.
        /// </summary>
        [NotNull]
        public AsyncSocket Socket { get; }
    }

    /// <summary>
    /// A subclass of <see cref="NetMQMonitorEventArgs"/> that also holds an error code.
    /// </summary>
    public class NetMQMonitorErrorEventArgs : NetMQMonitorEventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorErrorEventArgs that contains the given monitor, address, and error-code.
        /// </summary>
        /// <param name="monitor">The <see cref="NetMQMonitor"/> that raised this event.</param>
        /// <param name="address">The address of the event.</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        /// <param name="errorCode">The error code associated with this event.</param>
        public NetMQMonitorErrorEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, ErrorCode errorCode, SocketEvents socketEvent)
            : base(monitor, address, socketEvent)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Gets the error code associated with this event.
        /// </summary>
        public ErrorCode ErrorCode { get; }
    }

    /// <summary>
    /// A subclass of <see cref="NetMQMonitorEventArgs"/> that also holds an interval.
    /// </summary>
    public class NetMQMonitorIntervalEventArgs : NetMQMonitorEventArgs
    {
        /// <summary>
        /// Create a new NetMQMonitorIntervalEventArgs containing the given NetMQMonitor, address, and interval.
        /// </summary>
        /// <param name="monitor">the NetMQMonitor</param>
        /// <param name="address">The a string denoting the address</param>
        /// <param name="interval">The interval, in milliseconds.</param>
        /// <param name="socketEvent">The type of socket event that occurred.</param>
        public NetMQMonitorIntervalEventArgs([NotNull] NetMQMonitor monitor, [NotNull] string address, int interval, SocketEvents socketEvent)
            : base(monitor, address, socketEvent)
        {
            Interval = interval;
        }

        /// <summary>
        /// Gets the interval, in milliseconds.
        /// </summary>
        public int Interval { get; }
    }
}
