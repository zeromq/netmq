using JetBrains.Annotations;

namespace NetMQ.Devices
{
    /// <summary>
    /// The DeviceRunner (and it's subclass, ThreadedDeviceRunner) provides a platform-neutral way to call the Run method on a device.
    /// This is the non-threaded version.
    /// Depending upon the threading model, either this or a ThreadedDeviceRunner is used with a <see cref="IDevice"/>.
    /// </summary>
    internal class DeviceRunner
    {
        protected readonly IDevice Device;

        /// <summary>
        /// Create a new DeviceRunner object that contains the given IDevice.
        /// </summary>
        /// <param name="device">the IDevice that this DeviceRunner will contain</param>
        public DeviceRunner([NotNull] IDevice device)
        {
            Device = device;
        }

        /// <summary>
        /// Starts the device by calling it's Run method.
        /// </summary>
        public virtual void Start()
        {
            Device.Run();
        }
    }
}