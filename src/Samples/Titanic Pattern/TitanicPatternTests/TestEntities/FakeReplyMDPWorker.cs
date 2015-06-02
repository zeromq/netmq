using System;
using System.Threading;
using MDPCommons;
using NetMQ;

namespace TitanicProtocolTests.TestEntities
{
    public class FakeReplyMDPWorker : IMDPWorker
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
            // on the second call it could be
            //      a) [Ok][service][reply]
            //      b) [Pending]
            //      c) [Unknown]
            Reply = reply;

            if (ReferenceEquals (reply, null))
            {
                // this is the initiation call
                // so return the request to make when allowed
                waitHandle.WaitOne ();

                return Request;     // Guid of the request who's reply is asked for
            }

            waitHandle.WaitOne ();

            return null;     // will result in a dieing TitanicReply Thread
        }
    }
}
