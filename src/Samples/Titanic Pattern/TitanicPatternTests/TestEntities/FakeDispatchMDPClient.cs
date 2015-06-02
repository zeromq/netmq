using System;
using System.Text;
using System.Threading;

using MDPCommons;
using NetMQ;

namespace TitanicProtocolTests.TestEntities
{
    public class FakeDispatchMDPClient : IMDPClient
    {
        private int m_count;

        public readonly AutoResetEvent waitHandle = new AutoResetEvent (false);

        public void Dispose () { return; }

        public TimeSpan Timeout { get; set; }

        public int Retries { get; set; }

        public string Address { get { return "NO ADDRESS"; } }

        public byte[] Identity { get { return Encoding.UTF8.GetBytes ("NO IDENTITY"); } }

        public NetMQMessage Send (string serviceName, NetMQMessage request)
        {
            // first call is [mmi.service][servicename]
            // return is [Ok]
            // second call is [service][request]
            // return is [reply]
            m_count++;

            if (m_count == 1)
            {
                // proceed only if commanded to -> is automatically called by TitanicBroker.Run
                // and askes for a reply from a service, so wait until we want an answer
                waitHandle.WaitOne ();

                var reply = new NetMQMessage ();
                reply.Push (MmiCode.Ok.ToString ());

                return reply;
            }

            // wait to proceed until signaled
            waitHandle.WaitOne ();

            return request; // as echo service :-)
        }

#pragma warning disable 67
        public event EventHandler<MDPLogEventArgs> LogInfoReady;
#pragma warning restore 67
    }
}
