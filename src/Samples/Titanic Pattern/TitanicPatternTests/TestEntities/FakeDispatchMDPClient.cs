using System;
using System.Threading;

using MajordomoProtocol;
using MajordomoProtocol.Contracts;

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

        public event EventHandler<MDPLogEventArgs> LogInfoReady;
    }
}
