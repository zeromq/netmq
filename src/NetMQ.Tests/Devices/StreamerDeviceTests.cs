using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.Devices;
using NetMQ.zmq;

namespace NetMQ.Tests.Devices
{
	[TestFixture]
	public class StreamerDeviceTests
	{
		private const string Frontend = "inproc://front.addr";
		private const string Backend = "inproc://back.addr";
		private Thread m_workerThread;
		private readonly Random m_random = new Random();
		private static volatile int _workerReceiveCount;

		[Test]
		public void Threaded_Single_Client() {
			_workerReceiveCount = 0;

			using (var ctx = Context.Create()) {
				var queue = new StreamerDevice(ctx, Frontend, Backend);
				queue.Start();

				StartWorker(ctx);

				StartClient(ctx, 0);

				// Alow worker to do its magic
				Thread.Sleep(100); 

				StopWorker();

				queue.Stop();

				Assert.AreEqual(1, _workerReceiveCount);
			}
		}

		[Test]
		public void Threaded_Multi_Client() {
			_workerReceiveCount = 0;

			using (var ctx = Context.Create()) {
				var queue = new QueueDevice(ctx, Frontend, Backend);
				queue.Start();

				StartWorker(ctx);

				var clients = new Task[4];

				for (var i = 0; i < clients.Length; i++) {
					var i1 = i;
					clients[i] = Task.Factory.StartNew(() => StartClient(ctx, i1), TaskCreationOptions.LongRunning);
				}

				Task.WaitAll(clients);

				// Alow worker to do its magic
				Thread.Sleep(100);

				StopWorker();

				queue.Stop();

				Assert.AreEqual(clients.Length, _workerReceiveCount);
			}
		}

		private void StartClient(Context context, int id) {
			const string value = "Hello World";
			var expected = value + " " + id;
			Console.WriteLine("Client: {0} Pushing: {1}", id, expected);
			var client = context.CreatePushSocket();
			client.Connect(Frontend);
			client.Send(expected);
			Thread.Sleep(m_random.Next(1, 50));
			client.Close();
		}

		protected void StartWorker(Context context) {
			m_workerThread = new Thread(Worker);
			m_workerThread.Start(context);
		}

		protected void StopWorker() {
			m_workerThread.Abort();
		}

		private static void Worker(object p) {
			var ctx = (Context)p;

			var socket = ctx.CreatePullSocket();
			socket.Connect(Backend);

			while (true) {
				try {
					var has = socket.Poll(TimeSpan.FromMilliseconds(10), PollEvents.PollIn);

					if (!has) {
						Thread.Sleep(1);
						continue;
					}

					var received = socket.ReceiveAllString();
					Console.WriteLine("Worker received: ");

					for (var i = 0; i < received.Count; i++) {
						var r = received[i];
						Console.WriteLine("{0}: {1}", i, r);
					}

					Console.WriteLine("------");

					_workerReceiveCount++;
				} catch (ThreadAbortException) {
					socket.Close();
					return;
				}
			}
		}
	}
}