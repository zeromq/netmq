using zmq;

namespace ZeroMQ.Monitoring
{    

    /// <summary>
    /// Provides data for <see cref="ZmqMonitor.ConnectRetried"/> event.
    /// </summary>
    public class ZmqMonitorIntervalEventArgs : ZmqMonitorEventArgs
    {
        internal ZmqMonitorIntervalEventArgs(ZmqMonitor monitor, MonitorEvent data)
            : base(monitor, data.Addr)
        {
            this.Interval = (int)data.Arg;
        }

        /// <summary>
        /// Gets the computed reconnect interval.
        /// </summary>
        public int Interval { get; private set; }
    }
}