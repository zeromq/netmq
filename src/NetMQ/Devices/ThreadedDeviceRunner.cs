using System.Threading.Tasks;

namespace NetMQ.Devices
{
	/// <summary>
	/// Threaded version of the device runner. 
	/// </summary>
	internal class ThreadedDeviceRunner : DeviceRunner
	{
		public ThreadedDeviceRunner(IDevice device)
			: base(device) {
		}

		public override void Start() {
			Task.Factory.StartNew(Device.Run, TaskCreationOptions.LongRunning);
		}
	}
}