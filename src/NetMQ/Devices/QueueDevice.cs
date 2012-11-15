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
	public class QueueDevice : DeviceBase<RouterSocket, DealerSocket>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="QueueDevice"/> class that will run in a
		/// self-managed thread.
		/// </summary>
		/// <param name="context">The <see cref="Context"/> to use when creating the sockets.</param>
		/// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
		/// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
		public QueueDevice(Context context, string frontendBindAddress, string backendBindAddress,
			DeviceMode mode = DeviceMode.Threaded)
			: this(context, mode) {

			FrontendSocket.Bind(frontendBindAddress);
			BackendSocket.Bind(backendBindAddress);
		}

		private QueueDevice(Context context, DeviceMode mode)
			: base(context, context.CreateRouterSocket(), context.CreateDealerSocket(), mode) {
		}

		protected override void FrontendHandler(RouterSocket socket) {
			bool more;

			do {
				var data = socket.Receive(out more) ?? new byte[] { };

				if (more)
					BackendSocket.SendMore(data);
				else {
					BackendSocket.Send(data);
				}
			} while (more);
		}

		protected override void BackendHandler(DealerSocket socket) {
			bool more;

			do {
				var data = socket.Receive(out more) ?? new byte[] { };

				if (more)
					FrontendSocket.SendMore(data);
				else {
					FrontendSocket.Send(data);
				}
			} while (more);
		}
	}
}