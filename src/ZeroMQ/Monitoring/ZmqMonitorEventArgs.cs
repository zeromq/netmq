namespace ZeroMQ.Monitoring
{
    using System;

    /// <summary>
    /// A base class for the all ZmqMonitor events.
    /// </summary>
    public class ZmqMonitorEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqMonitorEventArgs"/> class.
        /// </summary>
        /// <param name="monitor">The <see cref="ZmqMonitor"/> that triggered the event.</param>
        /// <param name="address">The peer address.</param>
        public ZmqMonitorEventArgs(ZmqMonitor monitor, string address)
        {
            this.Monitor = monitor;
            this.Address = address;
        }

        /// <summary>
        /// Gets the monitor that triggered the event.
        /// </summary>
        public ZmqMonitor Monitor { get; private set; }

        /// <summary>
        /// Gets the peer address.
        /// </summary>
        public string Address { get; private set; }
    }
}