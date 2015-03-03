using System;
using NetMQ;

namespace MajordomoProtocol
{
    /// <summary>
    ///     a broker local representation of a connected worker
    /// </summary>
    internal class Worker
    {
        public string Id { get; private set; }              // the id of the worker as string
        public NetMQFrame Identity { get; private set; }    // identity of worker for routing
        public Service Service { get; set; }                // owing service if known
        public DateTime Expiry { get; set; }                // when does the worker expire, if no heartbeat

        public Worker (string id, NetMQFrame identity, Service service)
        {
            Id = id;
            Identity = identity;
            Service = service;
        }

        public override int GetHashCode ()
        {
            return (Id + Service.Name).GetHashCode ();
        }

        public override string ToString ()
        {
            return string.Format ("Name = {0} / Service = {1} / Expires {2}",
                                  Id,
                                  Service.Name,
                                  Expiry.ToShortTimeString ());
        }

        public override bool Equals (object obj)
        {
            if (ReferenceEquals (obj, null))
                return false;

            var other = obj as Worker;

            return !ReferenceEquals (other, null) && Id == other.Id && Service.Name == other.Service.Name;
        }
    }
}