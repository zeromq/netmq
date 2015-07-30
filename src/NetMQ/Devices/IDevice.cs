namespace NetMQ.Devices
{
    /// <summary>
    /// Interface IDevice represents a ZeroMQ device which connects a frontend socket to a backend socket.
    /// </summary>
    public interface IDevice
    {
        /// <summary>
        /// Get whether the device is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Start the device.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the device and safely close the underlying sockets.
        /// </summary>
        /// <param name="waitForCloseToComplete">If true, this method will block until the
        /// underlying poller is fully stopped. Defaults to true.</param>
        void Stop(bool waitForCloseToComplete = true);

        /// <summary>
        /// Start the device in the designated threading model.
        /// Should be used by implementations of the <see cref="DeviceRunner.Start"/> method.
        /// </summary>
        void Run();
    }
}