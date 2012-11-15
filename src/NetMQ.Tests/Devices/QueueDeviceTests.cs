using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.Devices;
using NetMQ.zmq;

namespace NetMQ.Tests.Devices
{
	[TestFixture]
	public class QueueDeviceTests
	{
		private const string Frontend = "inproc://front.addr";
		private const string Backend = "inproc://back.addr";
		private Thread m_workerThread;
		private readonly Random m_random = new Random();

		[Test]
		public void Threaded_Single_Client() {
			using (var ctx = Context.Create()) {
				var queue = new QueueDevice(ctx, Frontend, Backend);
				queue.Start();

				StartWorker(ctx);

				StartClient(ctx, 0);

				StopWorker();

				queue.Stop();
			}
		}

		[Test]
		public void Threaded_Multi_Client() {
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

				StopWorker();

				queue.Stop();
			}
		}

		private void StartClient(Context context, int id) {
			const string value = "Hello World";
			var expected = value + " " + id;
			Console.WriteLine("Client: {0} sending: {1}", id, expected);
			var client = context.CreateRequestSocket();
			client.Connect(Frontend);
			client.Send(expected);

			Thread.Sleep(m_random.Next(1, 50));

			var response = client.ReceiveAllString();
			Assert.AreEqual(1, response.Count);
			Assert.AreEqual(expected, response[0]);
			Console.WriteLine("Client: {0} received: {1}", id, response[0]);
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

			var socket = ctx.CreateResponseSocket();
			socket.Connect(Backend);

			while (true) {
				try {
					var has = socket.Poll(TimeSpan.FromMilliseconds(10), PollEvents.PollIn);

					if (!has) {
						Thread.Sleep(1);
						continue;
					}

					var received = socket.ReceiveAllString();

					for (var i = 0; i < received.Count; i++) {
						if (i == received.Count - 1) {
							socket.Send(received[i]);
						} else {
							socket.SendMore(received[i]);
						}
					}
				} catch (ThreadAbortException) {
					socket.Close();
					return;
				}
			}
		}
	}
}