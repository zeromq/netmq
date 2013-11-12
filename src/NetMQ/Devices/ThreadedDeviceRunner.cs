// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
#if !PRE_4
using System.Threading.Tasks;
#else
using System.Threading;
#endif

namespace NetMQ.Devices
{
    /// <summary>
    /// Threaded version of the device runner. 
    /// </summary>
    internal class ThreadedDeviceRunner : DeviceRunner
    {
        public ThreadedDeviceRunner(IDevice device)
            : base(device)
        {
        }

        /// <summary>
        /// Initiate execution of the Device's Run method on a new thread.
        /// </summary>
        public override void Start()
        {
#if !PRE_4

            Task.Factory.StartNew(Device.Run, TaskCreationOptions.LongRunning);

#else  // pre- .NET 3.5 did not have Tasks.  jh

            ThreadPool.QueueUserWorkItem(_ =>
                {
                    Device.Run();
                });
#endif
        }
    }
}