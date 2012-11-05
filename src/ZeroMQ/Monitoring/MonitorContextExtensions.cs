namespace ZeroMQ.Monitoring
{
    using System;

    /// <summary>
    /// Defines extension methods related to monitoring for <see cref="ZmqContext"/> instances.
    /// </summary>
    public static class MonitorContextExtensions
    {
        /// <summary>
        /// Creates a <see cref="ZmqMonitor"/> (a <see cref="SocketType.PAIR"/> socket) and connects
        /// it to the specified inproc monitoring endpoint.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that the <see cref="ZmqMonitor"/> socket and the socket it receives monitoring events
        /// from must belong to the same <see cref="ZmqContext"/>.
        /// </para>
        /// <para>
        /// The returned <see cref="ZmqMonitor"/> socket should be used in its own application thread.
        /// </para>
        /// <para>
        /// See <see cref="MonitorSocketExtensions.Monitor(ZeroMQ.ZmqSocket,string,MonitorEvents)"/> for
        /// enabling monitoring on a given socket.
        /// </para>
        /// </remarks>
        /// <param name="zmqContext">A <see cref="ZmqContext"/> instance.</param>
        /// <param name="endpoint">The endpoint to listen for state change events.</param>
        /// <returns>A <see cref="ZmqMonitor"/> instance that will listen for and raise state change events.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="zmqContext"/> or <see cref="endpoint"/> is null.</exception>
        /// <exception cref="ArgumentException"><see cref="endpoint"/> is an empty string.</exception>
        /// <exception cref="ZmqSocketException">An error occurred creating the monitor socket.</exception>
        public static ZmqMonitor CreateMonitorSocket(this ZmqContext zmqContext, string endpoint)
        {
            if (zmqContext == null)
            {
                throw new ArgumentNullException("zmqContext");
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to monitor to an empty endpoint.", "endpoint");
            }

            return new ZmqMonitor(zmqContext.CreateSocket(SocketType.PAIR), endpoint);
        }
    }
}
