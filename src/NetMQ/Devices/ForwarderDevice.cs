using NetMQ.Sockets;

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
    /// using the <see cref="DeviceBase.FrontendSetup"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var device = new ForwarderDevice(ctx,
    ///     "inproc://frontend",
    ///     "inproc://backend");
    ///
    /// device.FrontendSetup.Subscribe("topic");
    /// </code>
    /// </example>
    public class ForwarderDevice : DeviceBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public ForwarderDevice(string frontendBindAddress, string backendBindAddress,
            DeviceMode mode = DeviceMode.Threaded)
            : base(new SubscriberSocket(), new PublisherSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="poller">The <see cref="INetMQPoller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public ForwarderDevice(INetMQPoller poller, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode = DeviceMode.Threaded)
            : base(poller, new SubscriberSocket(), new PublisherSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }

        /// <summary>
        /// This override of FrontendHandler receives data from the socket contained within args,
        /// and Sends it to BackendSocket.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a Socket for receiving data from</param>
        protected override void FrontendHandler(object sender, NetMQSocketEventArgs args)
        {
            // TODO reuse a Msg instance here for performance
            bool more;

            do
            {
                var data = args.Socket.ReceiveFrameBytes(out more);

                if (more)
                    BackendSocket.SendMoreFrame(data);
                else
                    BackendSocket.SendFrame(data);
            } while (more);
        }
    }
}