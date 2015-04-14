using System;
using NetMQ;

namespace MDPCommons
{
    // define the possible message types for the worker MDP

    public interface IMDPWorker : IDisposable
    {
        /// <summary>
        ///     sen a heartbeat every specified milliseconds
        /// </summary>
        TimeSpan HeartbeatDelay { get; set; }

        /// <summary>
        ///     delay in milliseconds between reconnects
        /// </summary>
        TimeSpan ReconnectDelay { get; set; }

        /// <summary>
        ///     broadcast logging information via this event
        /// </summary>
        event EventHandler<MDPLogEventArgs> LogInfoReady;

        /// <summary>
        ///      sends it's reply and waits for a new request
        /// </summary>
        /// <param name="reply">reply to the received request</param>
        /// <returns>a request</returns>
        /// <remarks>
        ///      upon connection the first receive a worker does, he must
        ///      pass a <c>null</c> reply in order to initiate the REQ-REP
        ///      ping-pong
        /// </remarks>
        NetMQMessage Receive(NetMQMessage reply);
    }
}
