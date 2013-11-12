
namespace NetMQ.Devices
{
    /// <summary>
    /// Collects tasks from a set of pushers and forwards these to a set of pullers.
    /// </summary>
    /// <remarks>
    /// Generally used to bridge networks. Messages are fair-queued from pushers and
    /// load-balanced to pullers. This device is part of the pipeline pattern. The
    /// frontend speaks to pushers and the backend speaks to pullers.
    /// </remarks>
    public class StreamerDevice : DeviceBase
    {
        #region constructor (context, frontendBindAddress, backendBindAddress, mode)
        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class, of DeviceMode.Threaded.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <remarks>
        /// Note: This simply calls the equivalent method with DeviceMode.Threaded for the mode parameter.
        ///       This separate method-overload exists simply because C#/.NET earlier than 4.0 did not provide for default argument-values.
        /// </remarks>
        public StreamerDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress)
            : this(context, frontendBindAddress, backendBindAddress, DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public StreamerDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress,
                              DeviceMode mode)
            : base(context.CreatePullSocket(), context.CreatePushSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }
        #endregion

        #region constructor (context, poller, frontendBindAddress, backendBindAddress, mode)
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class, with a DevideMode of Threaded.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="poller">The <see cref="Poller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        /// <remarks>
        /// Note: This simply calls the equivalent method with DeviceMode.Threaded for the mode parameter.
        ///       This separate method-overload exists simply because C#/.NET earlier than 4.0 did not provide for default argument-values.
        /// </remarks>
        public StreamerDevice(NetMQContext context, Poller poller, string frontendBindAddress, string backendBindAddress)
            : this(context, poller, frontendBindAddress, backendBindAddress, DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="poller">The <see cref="Poller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>		
        public StreamerDevice(NetMQContext context, Poller poller, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode)
            : base(poller, context.CreatePullSocket(), context.CreatePushSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }
        #endregion

        #region FrontendHandler
        /// <summary>
        /// This Override of FrontendHandler receives data from the socket contained within args,
        /// and sends it to BackendSocket.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a Socket for receiving data from</param>
        protected override void FrontendHandler(object sender, NetMQSocketEventArgs args)
        {
            bool more;

            do
            {
                var data = args.Socket.Receive(out more);

                if (more)
                    BackendSocket.SendMore(data);
                else
                {
                    BackendSocket.Send(data);
                }
            } while (more);
        }
        #endregion
    }
}