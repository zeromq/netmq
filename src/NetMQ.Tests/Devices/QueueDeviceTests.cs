using System;
using System.Threading;
using NetMQ.Devices;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests.Devices
{

    public abstract class QueueDeviceTestBase : DeviceTestBase<QueueDevice, ResponseSocket>
    {
        protected override void SetupTest()
        {
            CreateDevice = () => new QueueDevice(Frontend, Backend);
            CreateClientSocket = () => new RequestSocket();
            //CreateWorkerSocket = c => c.CreateResponseSocket();
        }

        protected override void DoWork(NetMQSocket socket)
        {
            var received = socket.ReceiveMultipartStrings();

            for (var i = 0; i < received.Count; i++)
            {
                if (i == received.Count - 1)
                {
                    socket.SendFrame(received[i]);
                }
                else
                {
                    socket.SendMoreFrame(received[i]);
                }
            }
        }

        protected override void DoClient(int id, NetMQSocket socket)
        {
            const string value = "Hello World";
            var expected = value + " " + id;
            Console.WriteLine("Client: {0} sending: {1}", id, expected);
            socket.SendFrame(expected);

            Thread.Sleep(Random.Next(1, 50));

            var response = socket.ReceiveMultipartStrings();
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(expected, response[0]);
            Console.WriteLine("Client: {0} received: {1}", id, response[0]);
        }

        protected override ResponseSocket CreateWorkerSocket()
        {
            return new ResponseSocket();
        }
    }

    [TestFixture]
    public class QueueSingleClientTest : QueueDeviceTestBase
    {
        [Test]
        public void Run()
        {
            StartClient(0);

            SleepUntilWorkerReceives(1, TimeSpan.FromMilliseconds(100));

            StopWorker();
            Device.Stop();

            Assert.AreEqual(1, WorkerReceiveCount);
        }
    }

    [TestFixture]
    public class QueueMultiClientTest : QueueDeviceTestBase
    {
        [Test]
        public void Run()
        {
            for (var i = 0; i < 10; i++)
            {
                StartClient(i);
            }

            SleepUntilWorkerReceives(10, TimeSpan.FromMilliseconds(1000));

            StopWorker();
            Device.Stop();

            Assert.AreEqual(10, WorkerReceiveCount);
        }
    }
}