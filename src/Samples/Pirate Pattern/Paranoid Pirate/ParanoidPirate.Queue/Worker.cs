using System;

using NetMQ;

namespace ParanoidPirate.Queue
{
    public class Worker : IDisposable
    {
        public DateTime Expiry { get; set; }

        public NetMQFrame Identity { get; set; }

        public string Name
        {
            get { return Identity.ConvertToString (); }
            set { Identity = new NetMQFrame (value); }
        }

        public Worker (NetMQFrame id)
        {
            Identity = id;
            Expiry = DateTime.UtcNow + TimeSpan.FromMilliseconds (Commons.HEARTBEAT_INTERVAL * Commons.HEARTBEAT_LIVELINESS);
        }

        #region IDisposable

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Dispose (true);
        }

        protected void Dispose (bool disposing)
        {
            if (disposing)
                Identity = null;
        }

        #endregion IDisposable
    }
}
