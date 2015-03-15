using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Devices;
using NUnit.Framework;

namespace NetMQ.Tests.Devices
{
    public abstract class DeviceTestBase<TDevice, TWorkerSocket>
        where TDevice : IDevice
        where TWorkerSocket : NetMQSocket
    {
        protected const string Frontend = "inproc://front.addr";
        protected const string Backend = "inproc://back.addr";

        protected readonly Random Random = new Random();

        private NetMQContext m_context;
        protected TDevice Device;

        protected Func<NetMQContext, TDevice> CreateDevice;

        protected Func<NetMQContext, NetMQSocket> CreateClientSocket;
        protected abstract TWorkerSocket CreateWorkerSocket(NetMQContext context);

        protected int WorkerReceiveCount;

        private CancellationTokenSource m_workerCancellationSource;
        private CancellationToken m_workerCancellationToken;

        private ManualResetEvent m_workerDone;

        [SetUp]
        protected void SetUp()
        {
            WorkerReceiveCount = 0;
            m_workerDone = new ManualResetEvent(false);
            m_workerCancellationSource = new CancellationTokenSource();
            m_workerCancellationToken = m_workerCancellationSource.Token;

            m_context = NetMQContext.Create();
            SetupTest();
            Device = CreateDevice(m_context);
            Device.Start();

            StartWorker();
        }

        protected abstract void SetupTest();

        [TearDown]
        protected void TearDown()
        {
            m_context.Dispose();
        }

        protected abstract void DoWork(NetMQSocket socket);

        protected virtual void WorkerSocketAfterConnect(TWorkerSocket socket) { }

        protected void StartWorker()
        {
            Task.Factory.StartNew(() =>
            {
                using (var socket = CreateWorkerSocket(m_context))
                {
                    socket.Connect(Backend);
                    WorkerSocketAfterConnect(socket);

                    socket.ReceiveReady += (s, a) => { };
                    socket.SendReady += (s, a) => { };

                    while (!m_workerCancellationToken.IsCancellationRequested)
                    {
                        var has = socket.Poll(TimeSpan.FromMilliseconds(1));

                        if (!has)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        DoWork(socket);
                        Interlocked.Increment(ref WorkerReceiveCount);
                    }
                }

                m_workerDone.Set();
            }, TaskCreationOptions.LongRunning);
        }

        protected void StopWorker()
        {
            m_workerCancellationSource.Cancel();
            m_workerDone.WaitOne();
        }

        protected abstract void DoClient(int id, NetMQSocket socket);

        protected void StartClient(int id, int waitBeforeSending = 0)
        {
            Task.Factory.StartNew(() =>
            {
                using (var client = CreateClientSocket(m_context))
                {
                    client.Connect(Frontend);

                    if (waitBeforeSending > 0)
                        Thread.Sleep(waitBeforeSending);

                    DoClient(id, client);
                }
            });
        }

        protected void SleepUntilWorkerReceives(int messages, TimeSpan maxWait)
        {
            var start = DateTime.UtcNow + maxWait;
            while (WorkerReceiveCount != messages)
            {
                Thread.Sleep(1);

                if (DateTime.UtcNow <= start)
                    continue;

                Console.WriteLine("Max wait time exceeded for worker messages");
                return;
            }
        }
    }
}