using System;
using System.Threading;
using System.Threading.Tasks;

namespace MajordomoProtocol.Contracts
{
    public interface IMDPBroker : IDisposable
    {
        /// <summary>
        ///     sets or gets the timeout period for waiting for messages
        /// </summary>
        TimeSpan HeartbeatInterval { get; }

        /// <summary>
        ///     sets or gets the number of heartbeat cycles before the communication 
        ///     is deemed to be lost
        /// </summary>
        int HeartbeatLiveliness { get; set; }

        /// <summary>
        ///     broadcast logging information via this event
        /// </summary>
        event EventHandler<LogInfoEventArgs> LogInfoReady;

        /// <summary>
        ///     broadcast elaborate debugging info if subscribed to
        /// </summary>
        event EventHandler<LogInfoEventArgs> DebugInfoReady;

        /// <summary>
        ///     broker binds his socket to endpoint given at ctor
        ///     the broker must not have started operation
        /// </summary>
        /// <exception cref="ApplicationException">The bind operation failed. Most likely because 'endpoint' is malformed!</exception>
        /// <remarks>
        ///     broker uses the same endpoint to communicate with clients and workers(!)
        /// </remarks>
        void Bind();

        /// <summary>
        ///     broker binds to the specified endpoint, if already bound will unbind and
        ///     then bind to the endpoint
        ///     the broker must not have started operation!
        /// </summary>
        /// <param name="endpoint">new endpoint to bind to</param>
        /// <exception cref="InvalidOperationException">Can not change binding while operating!</exception>
        void Bind(string endpoint);

        /// <summary>
        ///     starts a broker on a previously or automatically bound socket/port and
        ///     processes messages according to Majordomo Protocol V0.1
        /// </summary>
        /// <param name="token">used to signal the broker to abandon</param>
        /// <exception cref="InvalidOperationException">Can not change binding while operating!</exception>
        Task Run(CancellationToken token);
    }
}