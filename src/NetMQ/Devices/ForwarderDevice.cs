
namespace NetMQ.Devices
{
    /// <summary>
    /// Collects messages from a set of publishers and forwards these to a set of subscribers.
    /// </summary>
    /// <remarks>
    /// Generally used to bridge networks. E.g. read on TCP unicast and forward on multicast.
    /// This device is part of the publish-subscribe pattern. The frontend speaks to publishers
    /// and the backend speaks to subscribers.
    /// In order to use the <see cref="ForwarderDevice"/> please make sure you subscribe the FrontendSocket 
    /// using the <see cref="ForwarderDevice.FrontendSetup"/>.
    /// </remarks>
    /// <example>
    /// var device = new ForwarderDevice(ctx, "inproc://frontend", "inproc://backend");
    /// device.FrontendSetup.Subscribe("topic");
    /// </example>
    public class ForwarderDevice : DeviceBase
    {
        #region constructor (context, frontendBindAddress, backendBindAddress, mode)
        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class, with a DeviceMode of Threaded.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <remarks>
        /// Note: This simply calls the equivalent method with DeviceMode.Threaded for the mode parameter.
        ///       This separate method-overload exists simply because C#/.NET earlier than 4.0 did not provide for default argument-values.
        /// </remarks>
        public ForwarderDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress)
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
        public ForwarderDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode)
            : base(context.CreateSubscriberSocket(), context.CreatePublisherSocket(), mode)
        {

            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }
        #endregion

        #region constructor (context, poller, frontendBindAddress, backendBindAddress, mode)
        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class with a DeviceMode of Threaded.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="poller">The <see cref="Poller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        public ForwarderDevice(NetMQContext context, Poller poller, string frontendBindAddress, string backendBindAddress)
            : this(context, poller, frontendBindAddress, backendBindAddress, DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="poller">The <see cref="Poller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device - defaults to Threaded if omitted.</param>		
        public ForwarderDevice(NetMQContext context, Poller poller, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode)
            : base(poller, context.CreateSubscriberSocket(), context.CreatePublisherSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }
        #endregion

        #region FrontendHandler
        /// <summary>
        /// This override of FrontendHandler receives data from the socket contained within args,
        /// and Sends it to BackendSocket.
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