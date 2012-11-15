using System;

namespace NetMQ.Devices
{

	/// <summary>
	/// Collects messages from a set of publishers and forwards these to a set of subscribers.
	/// </summary>
	/// <remarks>
	/// Generally used to bridge networks. E.g. read on TCP unicast and forward on multicast.
	/// This device is part of the publish-subscribe pattern. The frontend speaks to publishers
	/// and the backend speaks to subscribers.
	/// </remarks>
	public class ForwarderDevice : DeviceBase<SubscriberSocket, PublisherSocket>
	{

		/// <summary>
		/// Initializes a new instance of the <see cref="ForwarderDevice"/> class that will run in a
		/// self-managed thread.
		/// </summary>
		/// <param name="context">The <see cref="Context"/> to use when creating the sockets.</param>
		/// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
		/// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
		public ForwarderDevice(Context context, string frontendBindAddress, string backendBindAddress,
			DeviceMode mode = DeviceMode.Threaded)
			: this(context, mode) {

			FrontendSocket.Bind(frontendBindAddress);
			BackendSocket.Bind(backendBindAddress);
		}

		private ForwarderDevice(Context context, DeviceMode mode)
			: base(context, context.CreateSubscriberSocket(), context.CreatePublisherSocket(), mode) {
		}

		protected override void FrontendHandler(SubscriberSocket socket) {
			// TODO: the Publisher socket doesn't allow for sendmore semantics?
			throw new NotImplementedException();
		}
	}
}