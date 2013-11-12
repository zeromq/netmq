
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
        #region constructor (context, frontendBindAddress, backendBindAddress, mode)
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueDevice"/> class, with a DeviceMode of Threaded.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        /// <remarks>
        /// Note: This simply calls the equivalent method with DeviceMode.Threaded for the mode parameter.
        ///       This separate method-overload exists simply because C#/.NET earlier than 4.0 did not provide for default argument-values.
        /// </remarks>
        public QueueDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress)
            : this(context, frontendBindAddress, backendBindAddress, DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public QueueDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode)
            : base(context.CreateRouterSocket(), context.CreateDealerSocket(), mode)
        {
            FrontendSetup.Bind(frontendBindAddress);
            BackendSetup.Bind(backendBindAddress);
        }
        #endregion

        #region constructor (context, poller, frontendBindAddress, backendBindAddress, mode)
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueDevice"/> class with a DeviceMode of Threaded.
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
        public QueueDevice(NetMQContext context, Poller poller, string frontendBindAddress, string backendBindAddress)
            : this(context, poller, frontendBindAddress, backendBindAddress, DeviceMode.Threaded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueDevice"/> class.
        /// </summary>
        /// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
        /// <param name="poller">The <see cref="Poller"/> to use.</param>
        /// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
        /// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
        /// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
        public QueueDevice(NetMQContext context, Poller poller, string frontendBindAddress, string backendBindAddress,
            DeviceMode mode)
            : base(poller, context.CreateRouterSocket(), context.CreateDealerSocket(), mode)
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
                var data = args.Socket.Receive(out more) ?? new byte[] { };

                if (more)
                    BackendSocket.SendMore(data);
                else
                {
                    BackendSocket.Send(data);
                }
            } while (more);
        }
        #endregion

        #region BackendHandler
        /// <summary>
        /// This override of BackendHandler receives data from the socket contained within args,
        /// and Sends it to FrontendSocket.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a Socket for receiving data from</param>
        protected override void BackendHandler(object sender, NetMQSocketEventArgs args)
        {
            bool more;

            do
            {
                var data = args.Socket.Receive(out more) ?? new byte[] { };

                if (more)
                    FrontendSocket.SendMore(data);
                else
                {
                    FrontendSocket.Send(data);
                }
            } while (more);
        }
        #endregion
    }
}