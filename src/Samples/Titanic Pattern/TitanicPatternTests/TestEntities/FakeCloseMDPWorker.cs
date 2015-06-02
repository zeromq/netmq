using System;
using System.Threading;

using MDPCommons;
using NetMQ;

namespace TitanicProtocolTests.TestEntities
{
    public class FakeCloseMDPWorker : IMDPWorker
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
            if (ReferenceEquals (reply, null))
            {
                // upon the first call this is 'null'
                waitHandle.WaitOne ();
                // send back a Guid indicating the intended request to close
                return Request;
            }
            // reply should be [Ok]
            Reply = reply;

            return null;
        }
    }
}
