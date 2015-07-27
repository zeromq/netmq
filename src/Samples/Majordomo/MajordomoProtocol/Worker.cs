using System;
using NetMQ;

namespace MajordomoProtocol
{
    /// <summary>
    /// A broker local representation of a connected worker
    /// </summary>
    internal class Worker
    {
        // the id of the worker as string
        public string Id { get; }
        // identity of worker for routing
        public NetMQFrame Identity { get; private set; }
        // owing service if known
        public Service Service { get; set; }
        // when does the worker expire, if no heartbeat
        public DateTime Expiry { get; set; }

        public Worker(string id, NetMQFrame identity, Service service)
        {
            Id = id;
            Identity = identity;
            Service = service;
        }

        public override int GetHashCode()
        {
            return (Id + Service.Name).GetHashCode();
        }

        public override string ToString()
        {
            return $"Name = {Id} / Service = {Service.Name} / Expires {Expiry.ToShortTimeString()}";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            var other = obj as Worker;

            return !ReferenceEquals(other, null) && Id == other.Id && Service.Name == other.Service.Name;
        }
    }
}