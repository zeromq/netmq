using System;
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
		/// <param name="context">The <see cref="NetMQContext"/> to use when creating the sockets.</param>
		/// <param name="frontendBindAddress">The endpoint used to bind the frontend socket.</param>
		/// <param name="backendBindAddress">The endpoint used to bind the backend socket.</param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the device.</param>
		public StreamerDevice(NetMQContext context, string frontendBindAddress, string backendBindAddress,
		                      DeviceMode mode = DeviceMode.Threaded)
			: base( context.CreatePullSocket(), context.CreatePushSocket(), mode) {

			FrontendSetup.Bind(frontendBindAddress);
			BackendSetup.Bind(backendBindAddress);
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
			DeviceMode mode = DeviceMode.Threaded)
			: base(poller, context.CreatePullSocket(), context.CreatePushSocket(), mode) {

			FrontendSetup.Bind(frontendBindAddress);
			BackendSetup.Bind(backendBindAddress);
		}

		protected override void FrontendHandler(object sender, NetMQSocketEventArgs args)
		{
			bool more;

			do {
				var data = args.Socket.Receive(out more);

				if (more)
					BackendSocket.SendMore(data);
				else {
					BackendSocket.Send(data);
				}
			} while (more);
		}
	}
}