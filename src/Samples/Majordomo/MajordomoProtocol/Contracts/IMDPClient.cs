using System;
using NetMQ;

namespace MajordomoProtocol.Contracts
{
    public interface IMDPClient : IDisposable
    {
        /// <summary>
        ///     sets or gets the timeout period for waiting for messages
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        ///     sets or gets the number of tries before the communication 
        ///     is deemed to be lost
        /// </summary>
        int Retries { get; set; }

        /// <summary>
        ///     send a request to a broker for a specific service and receive the reply
        /// </summary>
        /// <param name="serviceName">the name of the service requested</param>
        /// <param name="request">the request message to process by service</param>
        /// <returns>the reply from service</returns>
        NetMQMessage Send(string serviceName, NetMQMessage request);

        /// <summary>
        ///     broadcast logging info via this event
        /// </summary>
        event EventHandler<LogInfoEventArgs> LogInfoReady;
    }
}