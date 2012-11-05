namespace ZeroMQ.Devices
{
    using System;

    /// <summary>
    /// Represents a ZeroMQ device, which connects a set of frontend sockets to a set of backend sockets.
    /// </summary>
    public interface IDevice : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the device loop is running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Start the device.
        /// </summary>
        void Start();

        /// <summary>
        /// Blocks the calling thread until the device terminates.
        /// </summary>
        void Join();

        /// <summary>
        /// Blocks the calling thread until the device terminates or the specified time elapses.
        /// </summary>
        /// <param name="timeout">
        /// A <see cref="TimeSpan"/> set to the amount of time to wait for the device to terminate.
        /// </param>
        /// <returns>
        /// true if the device terminated; false if the device has not terminated after
        /// the amount of time specified by <paramref name="timeout"/> has elapsed.
        /// </returns>
        bool Join(TimeSpan timeout);

        /// <summary>
        /// Stop the device in such a way that it can be restarted.
        /// </summary>
        void Stop();

        /// <summary>
        /// Stop the device and safely terminate the underlying sockets.
        /// </summary>
        void Close();
    }
}