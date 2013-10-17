namespace NetMQ.Devices
{
    /// <summary>
    /// Represents a ZeroMQ device, which connects a frontend socket to a backend socket.
    /// </summary>
    public interface IDevice
    {
        /// <summary>
        /// Gets whether the device is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Start the device.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the device and safely close the underlying sockets, with a value of true for waitForCloseToComplete.
        /// </summary>
        /// Note: This simply calls the Stop(bool) method with waitForCloseToComplete set to true.
        ///       This separate method-overload exists because C#/.NET earlier than 4.0 did not provide for default argument-values.
        /// </remarks>
        void Stop();

        /// <summary>
        /// Stop the device and safely close the underlying sockets.
        /// </summary>
        /// <param name="waitForCloseToComplete">If true, this method will block until the 
        /// underlying poller is fully stopped. Defaults to true.</param>		
        void Stop(bool waitForCloseToComplete);

        /// <summary>
        /// Start the device in the designated threading model. 
        /// Should be used by implementations of the <see cref="DeviceRunner.Start"/> method.
        /// </summary>		
        void Run();
    }
}