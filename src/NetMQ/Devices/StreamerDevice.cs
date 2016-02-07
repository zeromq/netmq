using NetMQ.Sockets;

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
        /// <summary>
        /// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
        /// </summary>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public StreamerDevice(string frontendBindAddress, string backendBindAddress,
                              DeviceMode mode = DeviceMode.Threaded)
            : base(new PullSocket(), new PushSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamerDevice"/> class.
        /// </summary>
        /// <param name="poller">The <see cref="INetMQPoller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public StreamerDevice(INetMQPoller poller, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode = DeviceMode.Threaded)
            : base(poller, new PullSocket(), new PushSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }

        /// <summary>
        /// This Override of FrontendHandler receives data from the socket contained within args,
        /// and sends it to BackendSocket.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a NetMqSocket for receiving data from</param>
        protected override void FrontendHandler(object sender, NetMQSocketEventArgs args)
        {
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