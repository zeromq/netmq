using System;
using MDPCommons;

namespace TitanicCommons
{
    /// <summary>
    ///     <para>Wraps the MDP Broker with a layer of TITANIC which does the following</para>
    ///     <para>+ writes messages to disc to ensure that none gets lost</para>
    ///     <para>+ good for sporadically connecting clients/workers</para>
    ///     <para>+ it uses the Majordomo Protocol</para>
    /// 
    ///     <para>it implements this broker asynchronous and handles all administrative work</para>
    ///     <para>if Run is called it automatically will Connect to the endpoint given</para>
    ///     <para>it however allows to alter that endpoint via Bind</para>
    ///     <para>it registers any worker with its service</para>
    ///     <para>it routes requests from clients to waiting workers offering the service the client has requested
    ///     as soon as they become available</para>
    /// 
    ///     <para>every client communicates with TITANIC and sends requests or a request for a reply
    ///     Titanic answers with either a GUID identifying the request or with a reply for a request
    ///     according to the transfered GUID</para>
    ///     <para>Titanic in turn handles the communication with the broker transparently for the client</para>
    ///     <para>Titanic is organized in three different services (threads)</para>
    ///         titanic.request -> storing the request and returning an GUID
    ///         titanic.reply   -> fetching a reply if one exists for an GUID and returning it
    ///         titanic.close   -> confirming that a reply has been stored and processed
    /// 
    ///     <para>every request is answered with a GUID by Titanic to the client and if a client asks for the result
    ///     of his request he must send this GUID to identify the respective request/answer</para>
    /// 
    ///     <para>Services can/must be requested with a request, a.k.a. data to process</para>
    /// 
    ///          CLIENT           CLIENT          CLIENT            CLIENT
    ///         "titanic,        "titanic,       "titanic,          "titanic,
    ///     give me Coffee"    give me Water"  give me Coffee"     give me Tea"
    ///            |                |               |                  |
    ///            +----------------+------+--------+------------------+
    ///                                    |
    ///                                    |
    ///                                  BROKER --------- TITANIC ------ DISC
    ///                                    |
    ///                                    |
    ///                        +-----------+-----------+
    ///                        |           |           |
    ///                      "Tea"     "Coffee"     "Water"
    ///                      WORKER     WORKER       WORKER
    /// 
    /// </summary>
    public interface ITitanicBroker : IDisposable
    {
        /// <summary>
        ///     event for any logging information
        /// </summary>
        /// <value>
        ///     any subscriber will receive detailed processing information
        ///     and can decide to process it as needed
        /// </value>
        event EventHandler<TitanicLogEventArgs> LogInfoReady;

        /// <summary>
        ///     starts the processing of TITANIC Broker
        ///     take optionally three workers for each main function of TitanicBroker
        /// </summary>
        /// <param name="requestWorker">MDP worker for processing requests</param>
        /// <param name="replyWorker">MDP worker for processing replies</param>
        /// <param name="closeWorker">MDP worker for processing close requests</param>
        /// <param name="serviceCallClient">MDP client to generate requests to MDP Broker for processing</param>
        /// <remarks>
        ///     <para>it creates three thread as MDP workers.</para>
        ///     <para>those are registering with a MDPBroker at a given IP address
        ///     for processing REQUEST, REPLY and Close requests send via MDP clients.</para>
        ///     <para>A mdp client is used to forward the requests made to titanic to the correct
        ///     service providing mdp worker and collect that's reply</para>
        ///     <para>the titanic commands are defined in TitanicCommons as TitanicOperation</para>
        /// </remarks>
        void Run (IMDPWorker requestWorker = null,
                  IMDPWorker replyWorker = null,
                  IMDPWorker closeWorker = null,
                  IMDPClient serviceCallClient = null);
    }
}
