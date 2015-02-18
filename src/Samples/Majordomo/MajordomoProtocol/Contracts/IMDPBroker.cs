using System;

using NetMQ;

namespace MajordomoProtocol.Contracts
{
    public interface IMDPBroker
    {
        /// <summary>
        ///     sets or gets the timeout period for waiting for messages
        /// </summary>
        TimeSpan HeartbeatInterval { get; set; }

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
        ///     broker binds his socket to this endpoint
        /// </summary>
        /// <remarks>
        ///     broker uses the same endpoint to communicate with clients and workers(!)
        /// </remarks>
        void Bind ();

        /// <summary>
        ///     process a READY, REPLY, HEARTBEAT, DISCONNECT message sent to the broker by a worker
        /// </summary>
        /// <param name="sender">the sender id</param>
        /// <param name="message">the message sent</param>
        void ProcessWorkerMessage (NetMQFrame sender, NetMQMessage message);

        /// <summary>
        ///     process REQUESt from a client. MMI requests are implemented here directly
        /// </summary>
        /// <param name="sender">client identity</param>
        /// <param name="message">the message received</param>
        void ProcessClientMessage (NetMQFrame sender, NetMQMessage message);

        /// <summary>
        ///     This method deletes any idle workers that haven't pinged us in a
        ///     while. We hold workers from oldest to most recent so we can stop
        ///     scanning whenever we find a live worker. This means we'll mainly stop
        ///     at the first worker, which is essential when we have large numbers of
        ///     workers (we call this method in our critical path)
        /// </summary>
        void Purge ();


        /// <summary>
        ///     send a heartbeat to all known and active worker
        /// </summary>
        void SendHeartbeat ();
    }
}
