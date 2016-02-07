using NetMQ.Sockets;

namespace NetMQ.Devices
{
    /// <summary>
    /// A shared queue that collects requests from a set of clients and distributes
    /// these fairly among a set of services.
    /// </summary>
    /// <remarks>
    /// Requests are fair-queued from frontend connections and load-balanced between
    /// backend connections. Replies automatically return to the client that made the
    /// original request. This device is part of the request-reply pattern. The frontend
    /// speaks to clients and the backend speaks to services.
    /// </remarks>
    public class QueueDevice : DeviceBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueDevice"/> class.
        /// </summary>
        /// <param name="poller">The <see cref="INetMQPoller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public QueueDevice(INetMQPoller poller, string frontendBindAddress, string backendBindAddress, DeviceMode mode = DeviceMode.Threaded)
            : base(poller, new RouterSocket(), new DealerSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueDevice"/> class.
        /// </summary>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public QueueDevice(string frontendBindAddress, string backendBindAddress, DeviceMode mode = DeviceMode.Threaded)
            : base(new RouterSocket(), new DealerSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }

        /// <summary>
        /// This override of FrontendHandler receives data from the socket contained within args,
        /// and Sends it to BackendSocket.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a NetMQSocket for receiving data from</param>
        protected override void FrontendHandler(object sender, NetMQSocketEventArgs args)
        {
            ForwardTo(args, BackendSocket);
        }

        /// <summary>
        /// This override of BackendHandler receives data from the socket contained within args,
        /// and Sends it to FrontendSocket.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a Socket for receiving data from</param>
        protected override void BackendHandler(object sender, NetMQSocketEventArgs args)
        {
            ForwardTo(args, FrontendSocket);
        }

        private static void ForwardTo(NetMQSocketEventArgs args, IOutgoingSocket toSocket)
        {
            var msg = new Msg();
            msg.InitEmpty();

            bool more;
            do
            {
                args.Socket.Receive(ref msg);
                more = msg.HasMore;
                toSocket.Send(ref msg, more);
            }
            while (more);

            msg.Close();
        }
    }
}