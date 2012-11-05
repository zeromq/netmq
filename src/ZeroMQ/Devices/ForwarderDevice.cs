namespace ZeroMQ.Devices
{
    /// <summary>
    /// Collects messages from a set of publishers and forwards these to a set of subscribers.
    /// </summary>
    /// <remarks>
    /// Generally used to bridge networks. E.g. read on TCP unicast and forward on multicast.
    /// This device is part of the publish-subscribe pattern. The frontend speaks to publishers
    /// and the backend speaks to subscribers.
    /// </remarks>
    public class ForwarderDevice : Device
    {
        /// <summary>
        /// The frontend <see cref="SocketType"/> for a forwarder device.
        /// </summary>
        public const SocketType FrontendType = SocketType.SUB;

        /// <summary>
        /// The backend <see cref="SocketType"/> for a forwarder device.
        /// </summary>
        public const SocketType BackendType = SocketType.PUB;

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class that will run in a
        /// self-managed thread.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddr">The address used to bind the frontend socket.</param>
        /// <param name="backendBindAddr">The endpoint used to bind the backend socket.</param>
        public ForwarderDevice(ZmqContext context, string frontendBindAddr, string backendBindAddr)
            : this(context)
        {
            FrontendSetup.Bind(frontendBindAddr);
            BackendSetup.Bind(backendBindAddr);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddr">The address used to bind the frontend socket.</param>
        /// <param name="backendBindAddr">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the current device.</param>
        public ForwarderDevice(ZmqContext context, string frontendBindAddr, string backendBindAddr, DeviceMode mode)
            : this(context, mode)
        {
            FrontendSetup.Bind(frontendBindAddr);
            BackendSetup.Bind(backendBindAddr);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class that will run in a
        /// self-managed thread.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        public ForwarderDevice(ZmqContext context)
            : base(context.CreateSocket(FrontendType), context.CreateSocket(BackendType), DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the current device.</param>
        public ForwarderDevice(ZmqContext context, DeviceMode mode)
            : base(context.CreateSocket(FrontendType), context.CreateSocket(BackendType), mode)
        {
        }

        /// <summary>
        /// Forwards requests from the frontend socket to the backend socket.
        /// </summary>
        /// <param name="args">A <see cref="SocketEventArgs"/> object containing the poll event args.</param>
        protected override void FrontendHandler(SocketEventArgs args)
        {
            FrontendSocket.Forward(BackendSocket);
        }

        /// <summary>
        /// Not implemented for the <see cref="ForwarderDevice"/>.
        /// </summary>
        /// <param name="args">A <see cref="SocketEventArgs"/> object containing the poll event args.</param>
        protected override void BackendHandler(SocketEventArgs args)
        {
        }
    }
}
