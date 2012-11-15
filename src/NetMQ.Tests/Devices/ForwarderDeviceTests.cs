using System;
using System.Threading;
using NUnit.Framework;
using NetMQ.Devices;
using NetMQ.zmq;

namespace NetMQ.Tests.Devices
{
	[TestFixture, Ignore("Broken at the moment.")]
	public class ForwarderDeviceTests
	{
		private const string Frontend = "inproc://front.addr";
		private const string Backend = "inproc://back.addr";
		private const string Topic = "Topic";

		private Thread m_workerThread;
		private static volatile int _workerReceiveCount;

		[Test]
		public void Threaded_Single_Client() {
			_workerReceiveCount = 0;

			using (var ctx = Context.Create()) {
				var queue = new ForwarderDevice(ctx, Frontend, Backend);
				queue.FrontendSetup.Subscribe(Topic);
				queue.Start();

				StartWorker(ctx);

				// Alow worker to start
				Thread.Sleep(100);

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
				var queue = new ForwarderDevice(ctx, Frontend, Backend);
				queue.FrontendSetup.Subscribe(Topic);
				queue.Start();

				// Alow worker to start				
				StartWorker(ctx);
				Thread.Sleep(100);

				var clients = new Thread[2];

				for (var i = 0; i < clients.Length; i++) {
					var i1 = i;
					clients[i] = new Thread(() => StartClient(ctx, i1));
					clients[i].Start();
				}

				// Alow worker to do its magic
				Thread.Sleep(5000);

				StopWorker();

				queue.Stop();
				Assert.AreEqual(clients.Length, _workerReceiveCount);
			}
		}

		private void StartClient(Context context, int id) {
			const string value = "Hello World";
			var expected = value + " " + id;
			Console.WriteLine("Client: {0} Publishing: {1}", id, expected);
			var client = context.CreatePublisherSocket();
			client.Connect(Frontend);

			client.SendTopic(Topic).Send(expected);
			Thread.Sleep(10);
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

			var socket = ctx.CreateSubscriberSocket();
			socket.Connect(Backend);
			socket.Subscribe(Topic);

			try {
				while (true) {

					var has = socket.Poll(TimeSpan.FromMilliseconds(1), PollEvents.PollIn);

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
				}
			} catch (ThreadAbortException) {
				socket.Close();
				return;
			}
		}
	}
}