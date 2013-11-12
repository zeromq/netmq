// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Threading;
#if !PRE_4
using System.Threading.Tasks;
#endif
using NUnit.Framework;
using NetMQ.Devices;

namespace NetMQ.Tests.Devices
{
    public abstract class DeviceTestBase<TDevice, TWorkerSocket>
        where TDevice : IDevice
        where TWorkerSocket : NetMQSocket
    {

        protected const string Frontend = "inproc://front.addr";
        protected const string Backend = "inproc://back.addr";

        protected readonly Random Random = new Random();

        protected NetMQContext Context;
        protected TDevice Device;

        protected Func<NetMQContext, TDevice> CreateDevice;

        protected Func<NetMQContext, NetMQSocket> CreateClientSocket;
        protected abstract TWorkerSocket CreateWorkerSocket(NetMQContext context);

        protected int WorkerReceiveCount;

#if !PRE_4
        private CancellationTokenSource m_workCancelationSource;
		private CancellationToken m_workerCancelationToken;
#else
        /// <summary>
        /// This is a flag that indicates a request has been made to stop (cancel) the socket monitoring.
        /// Zero represents false, 1 represents true - cancellation is requested.
        /// </summary>
        private long m_isCancellationRequested;
#endif

        protected ManualResetEvent WorkerDone;

        [TestFixtureSetUp]
        protected virtual void Initialize()
        {
            WorkerReceiveCount = 0;
            WorkerDone = new ManualResetEvent(false);

#if !PRE_4
            m_workCancelationSource = new CancellationTokenSource();
            m_workerCancelationToken = m_workCancelationSource.Token;
#endif

            Context = NetMQContext.Create();
            SetupTest();
            Device = CreateDevice(Context);
            Device.Start();

            StartWorker();
        }

        protected abstract void SetupTest();

        [TestFixtureTearDown]
        protected virtual void Cleanup()
        {
            Context.Dispose();
        }

        protected abstract void DoWork(NetMQSocket socket);

        protected virtual void WorkerSocketAfterConnect(TWorkerSocket socket) { }

        protected void StartWorker()
        {
#if !PRE_4
            Task.Factory.StartNew(() =>
#else
            new Thread(_ =>
#endif
            {
                var socket = CreateWorkerSocket(Context);
                socket.Connect(Backend);
                WorkerSocketAfterConnect(socket);

                socket.ReceiveReady += (s, a) => { };
                socket.SendReady += (s, a) => { };

                while (!IsCancellationRequested)
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

                socket.Close();

                WorkerDone.Set();
#if !PRE_4
            }, TaskCreationOptions.LongRunning);
#else
            }) { IsBackground = true, Name = "DeviceTestWorkerThread" }.Start();
#endif
        }

        protected void StopWorker()
        {
            RequestCancellation();
            WorkerDone.WaitOne();
        }

        #region IsCancellationRequested
        /// <summary>
        /// Get whether a request to cancel the socket-monitoring has been made.
        /// </summary>
        private bool IsCancellationRequested
        {
            get
            {
#if !PRE_4
                return m_workCancelationSource.IsCancellationRequested;
#else
                return Interlocked.Read(ref m_isCancellationRequested) != 0;
#endif
            }
        }
        #endregion

        #region RequestCancellation
        /// <summary>
        /// Set a flag that indicates that we want to stop ("cancel") monitoring the socket.
        /// </summary>
        private void RequestCancellation()
        {
#if !PRE_4
            m_workCancelationSource.Cancel();
#else
            // Set the cancellation-flag to the value that we are using to represent true.
            Interlocked.Exchange(ref m_isCancellationRequested, 1);
#endif
        }
        #endregion

        protected abstract void DoClient(int id, NetMQSocket socket);

        protected void StartClient(int id, int waitBeforeSending)
        {
#if !PRE_4
            Task.Factory.StartNew(() =>
            {
                var client = CreateClientSocket(Context);
                client.Connect(Frontend);

                if (waitBeforeSending > 0)
                    Thread.Sleep(waitBeforeSending);

                DoClient(id, client);
                client.Close();
            });

#else // pre-4.0 .NET did not have Tasks

            new Thread(_ =>
            {
                var client = CreateClientSocket(Context);
                client.Connect(Frontend);

                if (waitBeforeSending > 0)
                    Thread.Sleep(waitBeforeSending);

                DoClient(id, client);
                client.Close();
            }) { IsBackground = true, Name = "DeviceTestStartClientThread" }.Start();
#endif
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