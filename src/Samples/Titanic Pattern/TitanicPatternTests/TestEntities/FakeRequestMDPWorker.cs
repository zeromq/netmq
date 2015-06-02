using System;
using System.Threading;
using MDPCommons;
using NetMQ;

namespace TitanicProtocolTests.TestEntities
{
    public class FakeRequestMDPWorker : IMDPWorker
    {
        public readonly AutoResetEvent waitHandle = new AutoResetEvent (false);

        public NetMQMessage Request { get; set; }
        public NetMQMessage Reply { get; set; }

        public void Dispose () { return; }

        public TimeSpan HeartbeatDelay { get; set; }

        public TimeSpan ReconnectDelay { get; set; }

#pragma warning disable 67
        public event EventHandler<MDPLogEventArgs> LogInfoReady;
#pragma warning restore 67

        public NetMQMessage Receive (NetMQMessage reply)
        {
            // upon the first call this is 'null'
            if (ReferenceEquals (reply, null))
                return Request;     // [service][request]

            // on the second call it should be [Ok][Guid]
            Reply = reply;

            waitHandle.WaitOne ();

            return null;     // will result in a dieing TitanicRequest Thread
        }
    }
}
