namespace ZeroMQ.Monitoring
{
    using System;

    /// <summary>
    /// Defines extension methods related to monitoring for <see cref="ZmqSocket"/> instances.
    /// </summary>
    public static class MonitorSocketExtensions
    {
        /// <summary>
        /// Spawns a <see cref="SocketType.PAIR"/> socket that publishes all state changes (events) for
        /// the specified socket over the inproc transport at the given endpoint.
        /// </summary>
        /// <remarks>
        /// It is recommended to connect via a <see cref="SocketType.PAIR"/> socket in another thread
        /// to handle incoming monitoring events. The <see cref="ZmqMonitor"/> class provides an event-driven
        /// abstraction over event processing.
        /// </remarks>
        /// <param name="socket">The <see cref="ZmqSocket"/> instance to monitor for state changes.</param>
        /// <param name="endpoint">The inproc endpoint on which state changes will be published.</param>
        /// <exception cref="ArgumentNullException"><paramref name="socket"/> or <see cref="endpoint"/> is null.</exception>
        /// <exception cref="ArgumentException"><see cref="endpoint"/> is an empty string.</exception>
        /// <exception cref="ZmqSocketException">An error occurred initiating socket monitoring.</exception>
        public static void Monitor(this ZmqSocket socket, string endpoint)
        {
            Monitor(socket, endpoint, MonitorEvents.AllEvents);
        }

        /// <summary>
        /// Spawns a <see cref="SocketType.PAIR"/> socket that publishes the specified state changes (events) for
        /// the specified socket over the inproc transport at the given endpoint.
        /// </summary>
        /// <remarks>
        /// It is recommended to connect via a <see cref="SocketType.PAIR"/> socket in another thread
        /// to handle incoming monitoring events. The <see cref="ZmqMonitor"/> class provides an event-driven
        /// abstraction over event processing.
        /// </remarks>
        /// <param name="socket">The <see cref="ZmqSocket"/> instance to monitor for state changes.</param>
        /// <param name="endpoint">The inproc endpoint on which state changes will be published.</param>
        /// <param name="eventsToMonitor">
        /// A bitwise combination of <see cref="MonitorEvents"/> values indicating which event types should be published.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="socket"/> or <see cref="endpoint"/> is null.</exception>
        /// <exception cref="ArgumentException"><see cref="endpoint"/> is an empty string.</exception>
        /// <exception cref="ZmqSocketException">An error occurred initiating socket monitoring.</exception>
        public static void Monitor(this ZmqSocket socket, string endpoint, MonitorEvents eventsToMonitor)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to publish socket events to an empty endpoint.", "endpoint");
            }

            ZmqSocket.HandleProxyResult(socket.SocketProxy.Monitor(endpoint, (int)eventsToMonitor));
        }
    }
}
