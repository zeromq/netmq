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
            get { return Identity.ConvertToString(); }
            set { Identity = new NetMQFrame(value); }
        }

        public Worker(NetMQFrame id)
        {
            Identity = id;
            Expiry = DateTime.UtcNow + TimeSpan.FromMilliseconds(Commons.HeartbeatInterval*Commons.HeartbeatLiveliness);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Identity = null;
        }
    }
}