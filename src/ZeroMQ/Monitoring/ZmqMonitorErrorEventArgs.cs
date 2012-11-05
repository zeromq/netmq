using zmq;

namespace ZeroMQ.Monitoring
{    

    /// <summary>
    /// Provides data for <see cref="ZmqMonitor.ConnectDelayed"/>, <see cref="ZmqMonitor.AcceptFailed"/>,
    /// <see cref="ZmqMonitorErrorEventArgs"/> and <see cref="ZmqMonitor.BindFailed"/> events.
    /// </summary>
    public class ZmqMonitorErrorEventArgs : ZmqMonitorEventArgs
    {
        internal ZmqMonitorErrorEventArgs(ZmqMonitor monitor, MonitorEvent data)
            : base(monitor, data.Addr)
        {
            this.ErrorCode = (int)data.Arg;
        }

        /// <summary>
        /// Gets error code number.
        /// </summary>
        public int ErrorCode { get; private set; }
    }
}