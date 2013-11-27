using System.Threading;

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
            new Thread(Device.Run).Start();
		}
	}
}