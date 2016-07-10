#if !NET35

using System.Threading.Tasks;

namespace NetMQ.Devices
{
    /// <summary>
    /// This subclass of DeviceRunner is the threaded version.
    /// Depending upon the threading model, either this or a DeviceRunner is used with a <see cref="IDevice"/>.
    /// The distinction is simply that it invokes the IDevice.Run method in a new Task.
    /// </summary>
    internal class ThreadedDeviceRunner : DeviceRunner
    {
        /// <summary>
        /// Create a new ThreadedDeviceRunner object that contains the given IDevice.
        /// </summary>
        /// <param name="device">the IDevice that this ThreadedDeviceRunner will contain</param>
        public ThreadedDeviceRunner(IDevice device)
            : base(device)
        {
        }

        /// <summary>
        /// Start the device by calling it's Run method within a new Task.
        /// </summary>
        public override void Start()
        {
            Task.Factory.StartNew(Device.Run, TaskCreationOptions.LongRunning);
        }
    }
}
#endif