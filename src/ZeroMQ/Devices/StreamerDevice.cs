namespace ZeroMQ.Devices
{
    /// <summary>
    /// Collects tasks from a set of pushers and forwards these to a set of pullers.
    /// </summary>
    /// <remarks>
    /// Generally used to bridge networks. Messages are fair-queued from pushers and
    /// load-balanced to pullers. This device is part of the pipeline pattern. The
    /// frontend speaks to pushers and the backend speaks to pullers.
    /// </remarks>
    public class StreamerDevice : Device
    {
        /// <summary>
        /// The frontend <see cref="SocketType"/> for a streamer device.
        /// </summary>
        public const SocketType FrontendType = SocketType.PULL;

        /// <summary>
        /// The backend <see cref="SocketType"/> for a streamer device.
        /// </summary>
        public const SocketType BackendType = SocketType.PUSH;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class that will run in
        /// a self-managed thread.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddr">The address used to bind the frontend socket.</param>
        /// <param name="backendBindAddr">The endpoint used to bind the backend socket.</param>
        public StreamerDevice(ZmqContext context, string frontendBindAddr, string backendBindAddr)
            : this(context)
        {
            FrontendSetup.Bind(frontendBindAddr);
            BackendSetup.Bind(backendBindAddr);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddr">The address used to bind the frontend socket.</param>
        /// <param name="backendBindAddr">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the current device.</param>
        public StreamerDevice(ZmqContext context, string frontendBindAddr, string backendBindAddr, DeviceMode mode)
            : this(context, mode)
        {
            FrontendSetup.Bind(frontendBindAddr);
            BackendSetup.Bind(backendBindAddr);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class that will run in
        /// a self-managed thread.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        public StreamerDevice(ZmqContext context)
            : base(context.CreateSocket(FrontendType), context.CreateSocket(BackendType), DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="ZmqContext"/> to use when creating the sockets.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the current device.</param>
        public StreamerDevice(ZmqContext context, DeviceMode mode)
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
        /// Not implemented for the <see cref="StreamerDevice"/>.
        /// </summary>
        /// <param name="args">A <see cref="SocketEventArgs"/> object containing the poll event args.</param>
        protected override void BackendHandler(SocketEventArgs args)
        {
        }
    }
}
