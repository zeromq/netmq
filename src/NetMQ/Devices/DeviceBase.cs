using System;
using JetBrains.Annotations;

namespace NetMQ.Devices
{
    /// <summary>
    /// A DeviceBase forwards messages received by a front-end socket to a back-end socket, from which
    /// they are then sent, and can contain a Poller.
    /// </summary>
    public abstract class DeviceBase : IDevice
    {
        /// <summary>
        /// The frontend socket that will normally pass messages to <see cref="BackendSocket"/>.
        /// </summary>
        protected readonly NetMQSocket FrontendSocket;

        /// <summary>
        /// The backend socket that will normally receive messages from (and possibly send replies to) <see cref="FrontendSocket"/>.
        /// </summary>
        protected readonly NetMQSocket BackendSocket;

        /// <summary>
        /// This is the Poller which will handle socket coordination.
        /// </summary>
        private readonly Poller m_poller;

        /// <summary>
        /// This holds the IDevice and provides a platform-neutral way to call it's Run method,
        /// depending upon which threading-model is being used.
        /// </summary>
        private readonly DeviceRunner m_runner;

        /// <summary>
        /// This flag indicates whether this device owns the m_poller.
        /// </summary>
        private readonly bool m_pollerIsOwned;

        /// <summary>
        /// Get whether this device is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Get a <see cref="DeviceSocketSetup"/> for configuring the frontend socket.
        /// </summary>
        public DeviceSocketSetup FrontendSetup { get; private set; }

        /// <summary>
        /// Get a <see cref="DeviceSocketSetup"/> for configuring the backend socket.
        /// </summary>
        public DeviceSocketSetup BackendSetup { get; private set; }

        /// <summary>
        /// Create a new instance of the <see cref="DeviceBase"/> class.
        /// </summary>		
        /// <param name="frontendSocket">
        /// A <see cref="NetMQSocket"/> that will pass incoming messages to <paramref name="backendSocket"/>.
        /// </param>
        /// <param name="backendSocket">
        /// A <see cref="NetMQSocket"/> that will receive messages from (and optionally send replies to) <paramref name="frontendSocket"/>.
        /// </param>
        /// <param name="mode">the <see cref="DeviceMode"/> (either Blocking or Threaded) for this device</param>
        protected DeviceBase(NetMQSocket frontendSocket, NetMQSocket backendSocket, DeviceMode mode)
            : this(new Poller(), frontendSocket, backendSocket, mode)
        {
            m_pollerIsOwned = true;
        }

        /// <summary>
        /// Create a new instance of the <see cref="DeviceBase"/> class.
        /// </summary>
        /// <param name="poller">the <see cref="Poller"/> to use for detecting when messages are available</param>		
        /// <param name="frontendSocket">
        /// A <see cref="NetMQSocket"/> that will pass incoming messages to <paramref name="backendSocket"/>.
        /// </param>
        /// <param name="backendSocket">
        /// A <see cref="NetMQSocket"/> that will receive messages from (and optionally send replies to) <paramref name="frontendSocket"/>.
        /// </param>
        /// <param name="mode">the <see cref="DeviceMode"/> (either Blocking or Threaded) for this device</param>
        protected DeviceBase(Poller poller, [NotNull] NetMQSocket frontendSocket, [NotNull] NetMQSocket backendSocket, DeviceMode mode)
        {
            if (frontendSocket == null)
                throw new ArgumentNullException("frontendSocket");

            if (backendSocket == null)
                throw new ArgumentNullException("backendSocket");

            FrontendSocket = frontendSocket;
            BackendSocket = backendSocket;

            FrontendSetup = new DeviceSocketSetup(FrontendSocket);
            BackendSetup = new DeviceSocketSetup(BackendSocket);

            m_poller = poller;

            FrontendSocket.ReceiveReady += FrontendHandler;
            BackendSocket.ReceiveReady += BackendHandler;

            m_poller.AddSocket(FrontendSocket);
            m_poller.AddSocket(BackendSocket);

            m_runner = mode == DeviceMode.Blocking
                ? new DeviceRunner(this)
                : new ThreadedDeviceRunner(this);
        }

        /// <summary>
        /// Initiate operation of the Poller associated with this device,
        /// if this device owns it.
        /// This also sets IsRunning to true.
        /// </summary>
        public void Run()
        {
            if (m_pollerIsOwned && !m_poller.IsStarted)
                m_poller.PollTillCancelled();

            IsRunning = true;
        }

        /// <summary>
        /// Configure the frontend and backend sockets and then Start this device.
        /// </summary>
        public void Start()
        {
            FrontendSetup.Configure();
            BackendSetup.Configure();
            m_runner.Start();
        }

        /// <summary>
        /// Stop the device and safely close the underlying sockets.
        /// </summary>
        /// <param name="waitForCloseToComplete">If true, this method will block until the 
        /// underlying poller is fully stopped. Defaults to true.</param>		
        public void Stop(bool waitForCloseToComplete = true)
        {
            if (m_pollerIsOwned && m_poller.IsStarted)
            {
                if (waitForCloseToComplete)
                    m_poller.CancelAndJoin();
                else
                    m_poller.Cancel();
            }

            FrontendSocket.Close();
            BackendSocket.Close();
            IsRunning = false;
        }

        /// <summary>
        /// Invoked when a message has been received by the frontend socket.
        /// </summary>
        /// <param name="sender">the object that raised the ReceiveReady event</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a Socket for receiving data from</param>
        protected abstract void FrontendHandler(object sender, NetMQSocketEventArgs args);

        /// <summary>
        /// Invoked when a message has been received by the backend socket.
        /// </summary>
        /// <param name="sender">the object that raised the ReceiveReady event</param>
        /// <param name="args">a NetMQSocketEventArgs that contains a Socket for receiving data from</param>
        protected virtual void BackendHandler(object sender, NetMQSocketEventArgs args) { }
    }
}