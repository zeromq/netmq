using System;
using System.Threading.Tasks;
using NetMQ;
using TitanicCommons;

namespace TitanicProtocol
{
    public class TitanicClient:ITitanicClient
    {
        public TitanicClient ()
        { }

        public TitanicClient (string address, bool verbose)
            : this ()
        { }


        public NetMQMessage Send (string service, NetMQMessage request)
        {
            throw new NotImplementedException ();
        }

        public async Task<NetMQMessage> SendAsync (string service, NetMQMessage request)
        {
            throw new NotImplementedException ();
        }

        #region IDisposable

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        protected virtual void Dispose (bool disposing)
        {
            if (disposing)
            {
            //    // m_client might not have been created yet!
            //    if (!ReferenceEquals (m_client, null))
            //        m_client.Dispose ();

            //    m_ctx.Dispose ();
            }
            // get rid of unmanaged resources
        }

        #endregion IDisposable
    }
}
