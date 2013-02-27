using System;
using NetMQ.Sockets;

namespace NetMQ.Devices
{
	/// <summary>
	/// Forwards messages received by a front-end socket to a back-end socket, from which
	/// they are then sent.
	/// </summary>	
	public abstract class DeviceBase<TFront, TBack> : IDevice
		where TFront : ISocket
		where TBack : ISocket
	{

		/// <summary>
		/// The frontend socket that will normally pass messages to <see cref="BackendSocket"/>.
		/// </summary>
		protected readonly TFront FrontendSocket;

		/// <summary>
		/// The backend socket that will normally receive messages from (and possibly send replies to) <see cref="FrontendSocket"/>.
		/// </summary>
		protected readonly TBack BackendSocket;

		// Poller which will handle socket coordination.
		private readonly Poller m_poller;

		// Threading model to use
		private readonly DeviceRunner m_runner;

		// Does the device own the m_poller.
		private readonly bool m_pollerIsOwned;

		public bool IsRunning { get; private set; }

		/// <summary>
		/// Gets a <see cref="DeviceSocketSetup"/> for configuring the backend socket.
		/// </summary>
		public DeviceSocketSetup<TFront> FrontendSetup { get; private set; }

		/// <summary>
		/// Gets a <see cref="DeviceSocketSetup"/> for configuring the frontend socket.
		/// </summary>
		public DeviceSocketSetup<TBack> BackendSetup { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceBase"/> class.
		/// </summary>
		/// <param name="context"> </param>
		/// <param name="frontendSocket">
		/// A <see cref="BaseSocket"/> that will pass incoming messages to <paramref name="backendSocket"/>.
		/// </param>
		/// <param name="backendSocket">
		/// A <see cref="BaseSocket"/> that will receive messages from (and optionally send replies to) <paramref name="frontendSocket"/>.
		/// </param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the current device.</param>
		protected DeviceBase(Context context, TFront frontendSocket, TBack backendSocket, DeviceMode mode)
			: this(new Poller(context), frontendSocket, backendSocket, mode) {
			m_pollerIsOwned = true;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceBase"/> class.
		/// </summary>
		/// <param name="frontendSocket">
		/// A <see cref="BaseSocket"/> that will pass incoming messages to <paramref name="backendSocket"/>.
		/// </param>
		/// <param name="backendSocket">
		/// A <see cref="BaseSocket"/> that will receive messages from (and optionally send replies to) <paramref name="frontendSocket"/>.
		/// </param>
		/// <param name="mode">The <see cref="DeviceMode"/> for the current device.</param>
		/// <param name="poller">The <see cref="Poller"/> to use.</param>		
		protected DeviceBase(Poller poller, TFront frontendSocket, TBack backendSocket, DeviceMode mode) {
			if (frontendSocket == null)
				throw new ArgumentNullException("frontendSocket");

			if (backendSocket == null)
				throw new ArgumentNullException("backendSocket");

			FrontendSocket = frontendSocket;
			BackendSocket = backendSocket;

			FrontendSetup = new DeviceSocketSetup<TFront>(FrontendSocket);
			BackendSetup = new DeviceSocketSetup<TBack>(BackendSocket);

			m_poller = poller;

			m_poller.AddSocket(FrontendSocket, FrontendHandler);
			m_poller.AddSocket(BackendSocket, BackendHandler);

			m_runner = mode == DeviceMode.Blocking
				? new DeviceRunner(this)
				: new ThreadedDeviceRunner(this);
		}

		public void Start() {
			FrontendSetup.Configure();
			BackendSetup.Configure();
			m_runner.Start();
		}

		public void Stop(bool waitForCloseToComplete = true) {
			if(m_pollerIsOwned && m_poller.IsStarted)
				m_poller.Stop(waitForCloseToComplete);

			FrontendSocket.Close();
			BackendSocket.Close();
			IsRunning = false;
		}

		public void Run() {
			if (m_pollerIsOwned && !m_poller.IsStarted)
				m_poller.Start();

			IsRunning = true;
		}

		/// <summary>
		/// Invoked when a message has been received by the frontend socket.
		/// </summary>
		protected abstract void FrontendHandler(TFront socket);

		/// <summary>
		/// Invoked when a message has been received by the backend socket.
		/// </summary>
		protected virtual void BackendHandler(TBack socket) { }

	}
}