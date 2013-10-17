namespace NetMQ.Devices
{
	/// <summary>
	/// Non-threaded version of the device runner.
	/// Used for specifying the threading model to use with a <see cref="IDevice"/>.
	/// </summary>
	internal class DeviceRunner
	{
		protected readonly IDevice Device;

		public DeviceRunner(IDevice device) {
			Device = device;
		}

		/// <summary>
		/// Starts the device
		/// </summary>
		public virtual void Start() {
			Device.Run();
		}
	}
}