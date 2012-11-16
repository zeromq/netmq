using System;
using NUnit.Framework;
using NetMQ.Devices;

namespace NetMQ.Tests.Devices
{
	public abstract class StreamerDeviceTestBase : DeviceTestBase<StreamerDevice, PushSocket, PullSocket>
	{
		protected override void SetupTest() {
			CreateDevice = c => new StreamerDevice(c, Frontend, Backend);
			CreateClientSocket = c => c.CreatePushSocket();
			CreateWorkerSocket = c => c.CreatePullSocket();
		}

		protected override void DoWork(PullSocket socket) {
			var received = socket.ReceiveAllString();
			Console.WriteLine("Pulled: ");

			for (var i = 0; i < received.Count; i++) {
				var r = received[i];
				Console.WriteLine("{0}: {1}", i, r);
			}

			Console.WriteLine("------");
		}

		protected override void DoClient(int id, PushSocket socket) {
			const string value = "Hello World";
			var expected = value + " " + id;
			Console.WriteLine("({0}) Pushing: {1}", id, expected);
			socket.Send(expected);
		}
	}

	[TestFixture]
	public class StreamerMultiClientTest : StreamerDeviceTestBase
	{
		[Test]
		public void Run() {
			for (var i = 0; i < 10; i++) {
				StartClient(i);
			}

			SleepUntilWorkerReceives(10, TimeSpan.FromMilliseconds(1000));

			StopWorker();
			Device.Stop();

			Assert.AreEqual(10, WorkerReceiveCount);
		}		
	}

	[TestFixture]
	public class StreamerSingleClientTest : StreamerDeviceTestBase
	{
		[Test]
		public void Run() {
			StartClient(0);

			SleepUntilWorkerReceives(1, TimeSpan.FromMilliseconds(100));

			StopWorker();
			Device.Stop();

			Assert.AreEqual(1, WorkerReceiveCount);
		}	
	}
}