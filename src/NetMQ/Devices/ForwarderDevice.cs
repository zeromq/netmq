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
	/// In order to use the <see cref="ForwarderDevice"/> please make sure you subscribe the FrontendSocket 
	/// using the <see cref="ForwarderDevice.FrontendSetup"/>.
	/// </remarks>
	/// <example>
	/// var device = new ForwarderDevice(ctx, "inproc://frontend", "inproc://backend");
	/// device.FrontendSetup.Subscribe("topic");
	/// </example>
	public class ForwarderDevice : DeviceBase<SubscriberSocket, PublisherSocket>
	{

		/// <summary>
		/// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
		/// </summary>
		/// <param name="context">The <see cref="Context"/> to use when creating the sockets.</param>
		/// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
		/// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
		public ForwarderDevice(Context context, string frontendBindAddress, string backendBindAddress,
			DeviceMode mode = DeviceMode.Threaded)
			: this(context, mode) {

			FrontendSetup.Bind(frontendBindAddress);
			BackendSetup.Bind(backendBindAddress);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ForwarderDevice"/> class.
		/// </summary>
		/// <param name="context">The <see cref="Context"/> to use when creating the sockets.</param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>		
		public ForwarderDevice(Context context, DeviceMode mode = DeviceMode.Threaded)
			: base(context, context.CreateSubscriberSocket(), context.CreatePublisherSocket(), mode) {
		}

		protected override void FrontendHandler(SubscriberSocket socket) {
			bool more;

			PublisherSocket.PublisherSendMessage msg = null;

			do {
				var data = socket.Receive(out more);

				if (msg == null) {
					msg = BackendSocket.SendTopic(data);
				} else {
					if (more)
						msg.SendMore(data);
					else {
						msg.Send(data);
					}
				}
			} while (more);
		}
	}
}