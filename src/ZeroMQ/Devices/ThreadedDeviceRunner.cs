namespace ZeroMQ.Devices
{
    using System;
    using System.Threading;

    internal class ThreadedDeviceRunner : DeviceRunner
    {
        private readonly Thread _runThread;

        public ThreadedDeviceRunner(Device device)
            : base(device)
        {
            _runThread = new Thread(Device.Run);
        }

        public override void Start()
        {
            _runThread.Start();
        }

        public override void Join()
        {
            _runThread.Join();
        }

        public override bool Join(TimeSpan timeout)
        {
            return _runThread.Join(timeout);
        }
    }
}
