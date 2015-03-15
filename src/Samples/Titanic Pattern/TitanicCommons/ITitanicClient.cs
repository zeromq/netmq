using System;
using System.Threading.Tasks;
using NetMQ;

namespace TitanicCommons
{
    public interface ITitanicClient : IDisposable
    {
        /// <summary>
        ///     send a message and get a reply
        /// </summary>
        /// <param name="service">the service requested</param>
        /// <param name="request">the actual request</param>
        /// <returns>the reply</returns>
        NetMQMessage Send (string service, NetMQMessage request);

        /// <summary>
        ///     send a message and get a reply asynchronously
        /// </summary>
        /// <param name="service">the service requested</param>
        /// <param name="request">the actual request</param>
        Task<NetMQMessage> SendAsync (string service, NetMQMessage request);
    }
}
