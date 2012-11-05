namespace ZeroMQ.Devices
{
    using System;

    internal class DeviceRunner
    {
        protected readonly Device Device;

        public DeviceRunner(Device device)
        {
            Device = device;
        }

        public virtual void Start()
        {
            Device.Run();
        }

        public virtual void Join()
        {
            Device.DoneEvent.WaitOne();
        }

        public virtual bool Join(TimeSpan timeout)
        {
            return Device.DoneEvent.WaitOne(timeout);
        }
    }
}
