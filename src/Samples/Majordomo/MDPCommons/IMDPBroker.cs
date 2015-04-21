using System;
using System.Threading;
using System.Threading.Tasks;

namespace MDPCommons
{
    /// <summary>
    ///     Implements a Broker according to Majordomo Protocol v0.1
    ///     it implements this broker asynchronous and handles all administrative work
    ///     if Run is called it automatically will Connect to the endpoint given
    ///     it however allows to alter that endpoint via Bind
    ///     it registers any worker with its service
    ///     it routes requests from clients to waiting workers offering the service the client has requested
    ///     as soon as they become available
    /// 
    ///     Services can/must be requested with a request, a.k.a. data to process
    /// 
    ///          CLIENT     CLIENT       CLIENT     CLIENT
    ///         "Coffee"    "Water"     "Coffee"    "Tea"
    ///            |           |            |         |
    ///            +-----------+-----+------+---------+
    ///                              |
    ///                            BROKER
    ///                              |
    ///                  +-----------+-----------+
    ///                  |           |           |
    ///                "Tea"     "Coffee"     "Water"
    ///                WORKER     WORKER       WORKER
    /// 
    /// </summary>
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
        event EventHandler<MDPLogEventArgs> LogInfoReady;

        /// <summary>
        ///     broadcast elaborate debugging info if subscribed to
        /// </summary>
        event EventHandler<MDPLogEventArgs> DebugInfoReady;

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
