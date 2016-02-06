using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

using MajordomoProtocol;
using MDPCommons;
using NetMQ;
using NetMQ.Sockets;

using TitanicCommons;

namespace TitanicProtocol
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
    public class TitanicBroker : ITitanicBroker
    {
        private const string _titanic_internal_communication = "inproc://titanic.inproc";
        private const string _titanic_directory = ".titanic";

        private readonly string m_titanicAddress = "tcp://localhost:5555";

        /// <summary>
        ///     allows the access to the titanic storage
        /// </summary>
        private readonly ITitanicIO m_io;

        /// <summary>
        ///     returns the ip-address of the titanic broker
        /// </summary>
        public string TitanicAddress { get { return m_titanicAddress; } }

        /// <summary>
        ///     if broker has a log message available if fires this event
        /// </summary>
        public event EventHandler<TitanicLogEventArgs> LogInfoReady;

        /// <summary>
        ///     ctor
        ///         initializes the ip address to 'tcp://localhost:5555'
        ///         initializes root for storage "application directory"\.titanic
        ///         initializes io to TitanicFileIO
        /// </summary>
        public TitanicBroker ([CanBeNull] ITitanicIO titanicIO = null, string path = null)
        {
            var titanicDirectory = string.IsNullOrWhiteSpace (path)
                                            ? Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory)
                                            : path;

            m_io = titanicIO ?? new TitanicFileIO (titanicDirectory);
        }

        /// <summary>
        ///     ctor with broker ip
        ///
        ///         but does NOT create any directory or file
        /// </summary>
        /// <param name="endpoint">the ip address of the broker as string may include a port
        ///                  the format is: tcp://'ip address':'port'
        ///                  e.g. 'tcp://localhost:5555' or 'tcp://35.1.45.76:5000' or alike</param>
        /// <param name="path">point to the intended root path for the file system and must exist</param>
        /// <param name="titanicIO">a implementation of ITitanicIO allowing to persist</param>
        /// <exception cref="ApplicationException">The broker ip address is invalid!</exception>
        public TitanicBroker ([NotNull] string endpoint, string path = null, ITitanicIO titanicIO = null)
            : this (titanicIO, path)
        {
            m_titanicAddress = endpoint;
        }

        /// <summary>
        ///     <para>is the main thread of the broker</para>
        ///     <para>it spawns threads handling titanic operations</para>
        ///     <para>it receives GUID from Titanic Request Service and dispatches the requests
        ///     to available workers via MDPBroker</para>
        ///     <para>it also manages the appropriate changes in the file system as well as in queue</para>
        /// </summary>
        /// <param name="requestWorker">mdp worker processing the incoming requests for services</param>
        /// <param name="replyWorker">mdp worker processing incoming reply requests</param>
        /// <param name="closeWorker">mdp worker processing incoming close requests</param>
        /// <param name="serviceCallClient">mdp client forwarding requests to service providing mdp worker
        ///                                 via mdp broker and collecting replies</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener encountered an
        ///                                  error during initialization.</exception>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        public void Run ([CanBeNull] IMDPWorker requestWorker = null,
                         [CanBeNull] IMDPWorker replyWorker = null,
                         [CanBeNull] IMDPWorker closeWorker = null,
                         [CanBeNull] IMDPClient serviceCallClient = null)
        {
            using (var pipeStart = new PairSocket ())
            using (var pipeEnd = new PairSocket ())
            using (var cts = new CancellationTokenSource ())
            {
                // set up the inter thread communication pipe
                pipeStart.Bind (_titanic_internal_communication);
                pipeEnd.Connect (_titanic_internal_communication);

                // start the three child tasks
                var requestTask = Task.Run (() => ProcessTitanicRequest (pipeEnd, requestWorker), cts.Token);
                var replyTask = Task.Run (() => ProcessTitanicReply (replyWorker), cts.Token);
                var closeTask = Task.Run (() => ProcessTitanicClose (closeWorker), cts.Token);

                var tasks = new[] { requestTask, replyTask, closeTask };

                while (true)
                {
                    // wait for 1s for a new request from 'Request' to process
                    var input = pipeStart.Poll (PollEvents.PollIn, TimeSpan.FromMilliseconds (1000));

                    // any message available? -> process it
                    if ((input & PollEvents.PollIn) == PollEvents.PollIn)
                    {
                        // only one frame will be send [Guid]
                        var msg = pipeStart.ReceiveFrameString ();

                        Guid guid;
                        if (!Guid.TryParse (msg, out guid))
                            Log ("[TITANIC BROKER] Received a malformed GUID via pipe - throw it away");
                        else
                        {
                            Log (string.Format ("[TITANIC BROKER] Received request GUID {0} via pipe", msg));
                            // now we have a valid GUID - save it to disk for further use
                            m_io.SaveNewRequestEntry (guid);
                        }
                    }
                    //! now dispatch (brute force) the requests -> SHOULD BE MORE INTELLIGENT (!)
                    // dispatching will also worry about the handling of a potential reply
                    // dispatch only requests which have not been closed
                    foreach (var entry in m_io.GetNotClosedRequestEntries ().Where (entry => entry != default (RequestEntry)))
                    {
                        if (DispatchRequests (entry.RequestId, serviceCallClient))
                            m_io.SaveProcessedRequestEntry (entry);
                    }

                    //! should implement some sort of restart
                    // beware of the silently dieing threads - must be detected!
                    if (DidAnyTaskStopp (tasks))
                    {
                        // stop all threads
                        cts.Cancel ();
                        // stop processing!
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     process a titanic request according to TITANIC Protocol
        ///
        ///     <para>it connects via provided PAIR socket to main thread</para>
        ///     <para>write request to disk and return the GUID to client</para>
        ///     sends the GUID of the request back via the pipe for further processing
        /// </summary>
        internal void ProcessTitanicRequest ([NotNull] PairSocket pipe, [CanBeNull]IMDPWorker mdpWorker = null)
        {
            // get a MDP worker with an automatic id and register with the service "titanic.request"
            // the worker will automatically start and connect to a MDP Broker at the indicated address
            using (var worker = mdpWorker ?? new MDPWorker (m_titanicAddress, TitanicOperation.Request.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a 'null', since there is no initial reply
                    // a request should be [service name][request data]
                    var request = worker.Receive (reply);

                    Log (string.Format ("[TITANIC REQUEST] Received request: {0}", request));

                    //! has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    //! check if service exists! and return 'Unknown' if not

                    // generate Guid for the request
                    var requestId = Guid.NewGuid ();
                    // save request to file -> [service name][request data]
                    m_io.SaveMessage (TitanicOperation.Request, requestId, request);

                    Log (string.Format ("[TITANIC REQUEST] sending through pipe: {0}", requestId));

                    // send GUID through message queue to main thread
                    pipe.SendFrame (requestId.ToString ());
                    // return GUID via reply message via worker.Receive call
                    reply = new NetMQMessage ();
                    // [Guid]
                    reply.Push (requestId.ToString ());
                    // [Ok][Guid]
                    reply.Push (TitanicReturnCode.Ok.ToString ());

                    Log (string.Format ("[TITANIC REQUEST] sending reply: {0}", reply));
                }
            }
        }

        /// <summary>
        ///     process any titanic reply request by a client
        ///
        ///     <para>will send an OK, PENDING or UNKNOWN as result of the request for the reply</para>
        /// </summary>
        internal void ProcessTitanicReply ([CanBeNull] IMDPWorker mdpWorker = null)
        {
            // get a MDP worker with an automatic id and register with the service "titanic.reply"
            // the worker will automatically start and connect to the indicated address
            using (var worker = mdpWorker ?? new MDPWorker (m_titanicAddress, TitanicOperation.Reply.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication to MDP Broker with sending a 'null',
                    // since there is no initial reply everytime thereafter the reply will be send
                    var request = worker.Receive (reply);

                    Log (string.Format ("TITANIC REPLY] received: {0}", request));

                    //! has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    var requestIdAsString = request.Pop ().ConvertToString ();
                    var requestId = Guid.Parse (requestIdAsString);

                    if (m_io.ExistsMessage (TitanicOperation.Reply, requestId))
                    {
                        Log (string.Format ("[TITANIC REPLY] reply for request exists: {0}", requestId));

                        reply = m_io.GetMessage (TitanicOperation.Reply, requestId);    // [service][reply]
                        reply.Push (TitanicReturnCode.Ok.ToString ());                  // ["OK"][service][reply]
                    }
                    else
                    {
                        reply = new NetMQMessage ();

                        var replyCommand = (m_io.ExistsMessage (TitanicOperation.Request, requestId)
                                                ? TitanicReturnCode.Pending
                                                : TitanicReturnCode.Unknown);

                        reply.Push (replyCommand.ToString ());
                    }

                    Log (string.Format ("[TITANIC REPLY] reply: {0}", reply));
                }
            }
        }

        /// <summary>
        ///     an idempotent method processing all requests to close a request with a GUID
        ///     it is safe to call it multiple times with the same GUID
        /// </summary>
        internal void ProcessTitanicClose ([CanBeNull]IMDPWorker mdpWorker = null)
        {
            // get a MDP worker with an automatic id and register with the service "titanic.Close"
            // the worker will automatically start and connect to MDP Broker with the indicated address
            using (var worker = mdpWorker ?? new MDPWorker (m_titanicAddress, TitanicOperation.Close.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a null, since there is no reply yet
                    var request = worker.Receive (reply);

                    Log (string.Format ("[TITANIC CLOSE] received: {0}", request));

                    //! has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    // we expect [Guid] as the only frame
                    var guidAsString = request.Pop ().ConvertToString ();
                    var guid = Guid.Parse (guidAsString);

                    Log (string.Format ("[TITANIC CLOSE] closing {0}", guid));

                    // close the request
                    m_io.CloseRequest (guid);
                    // send back the confirmation
                    reply = new NetMQMessage ();
                    reply.Push (TitanicReturnCode.Ok.ToString ());
                }
            }
        }

        /// <summary>
        ///     dispatch the request with the specified GUID th the next available worker if any
        /// </summary>
        /// <param name="requestId">request's GUID</param>
        /// <param name="serviceClient">mdp client to handle mdp broker communication</param>
        /// <returns><c>true</c> if successful <c>false</c> otherwise</returns>
        internal bool DispatchRequests (Guid requestId, [CanBeNull] IMDPClient serviceClient = null)
        {
            // has the request already been processed? -> file does not exist
            // threat this as successfully processed
            if (!m_io.ExistsMessage (TitanicOperation.Request, requestId))
            {
                Log (string.Format ("[TITANIC DISPATCH] Request {0} does not exist. Removing it from queue.", requestId));
                // close request inorder to avoid any further processing
                m_io.CloseRequest (requestId);

                return true;
            }

            // load request from file
            var request = m_io.GetMessage (TitanicOperation.Request, requestId);
            // [service name][data]
            var serviceName = request[0].ConvertToString ();

            Log (string.Format ("[TITANIC DISPATCH] Do a ServiceCall for {0} - {1}.", serviceName, request));

            var reply = ServiceCall (serviceName, request, serviceClient);

            if (ReferenceEquals (reply, null))
                return false;       // no reply

            // a reply has been received -> save it
            Log (string.Format ("[TITANIC DISPATCH] Saving reply for request {0}.", requestId));
            // save the reply for further use
            m_io.SaveMessage (TitanicOperation.Reply, requestId, reply);

            return true;
        }

        /// <summary>
        ///     carry out the actual call to the worker via a broker in order
        ///     to process the request and get the reply which we wait for
        ///
        ///     <para>it creates a MDPClient and requests from MDPBroker the service.
        ///     if that service exists it uses the returned worker address and issues
        ///     a request with the appropriate data and waits for the reply</para>
        /// </summary>
        /// <param name="serviceName">the service's name requested</param>
        /// <param name="request">request to process by worker</param>
        /// <param name="serviceClient">mdp client handling the forwarding of requests
        ///             to service offering mdp worker via mdp broker and collecting
        ///             the replies from the mdp worker</param>
        /// <returns><c>true</c> if successful and <c>false</c> otherwise</returns>
        private NetMQMessage ServiceCall ([NotNull] string serviceName, [NotNull] NetMQMessage request, [CanBeNull]IMDPClient serviceClient = null)
        {
            // create MDPClient session and send the request to MDPBroker
            using (var session = serviceClient ?? new MDPClient (m_titanicAddress))
            {
                session.Timeout = TimeSpan.FromMilliseconds (1000);     // 1s
                session.Retries = 1;                                    // only 1 retry
                // use MMI protocol to check if service is available
                var mmi = new NetMQMessage ();
                // add name of service to inquire
                mmi.Push (serviceName);
                // request mmi.service resolution
                var reply = session.Send ("mmi.service", mmi);
                // first frame should be result of inquiry
                var rc = reply[0].ConvertToString ();
                var answer = (MmiCode) Enum.Parse (typeof (MmiCode), rc);
                // answer == "Ok" -> service is available -> make the request
                if (answer == MmiCode.Ok)
                {
                    Log (string.Format ("[TITANIC SERVICECALL] -> {0} - {1}", serviceName, request));

                    return session.Send (serviceName, request);
                }

                Log (string.Format ("[TITANIC SERVICECALL] Service {0} (RC = {1}/{2}) is not available.",
                                    serviceName,
                                    answer,
                                    request));

                //! shall this information be available for request? Unknown or Pending
                //! so TitanicRequest could utilize this information

                return null;
            }
        }

        /// <summary>
        ///     handle the situation when a thread dies and take appropriate steps
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        private bool DidAnyTaskStopp ([NotNull] Task[] tasks)
        {
            if (Task.WhenAny (tasks).IsCompleted)
            {
                Log (string.Format ("UNEXPECTED ABORTION OF A THREAD! ABANDONING!"));

                return true;
            }

            if (Task.WhenAny (tasks).IsFaulted)
            {
                Log (string.Format ("An exception has been detected! ABANDONING!"));
                // get the exceptions available and log them
                foreach (var task in tasks.Where (task => task.IsFaulted))
                    LogExceptions (task.Exception);

                return true;
            }

            return false;
        }

        private void LogExceptions (AggregateException exception)
        {
            Log (string.Format ("Exception: {0}", exception.Message));

            foreach (var ex in exception.Flatten ().InnerExceptions)
                Log (string.Format ("Inner Exception: {0}", ex.Message));
        }

        private void Log ([NotNull] string info)
        {
            OnLogInfoReady (new TitanicLogEventArgs { Info = "[TITANIC BROKER]" + info });
        }

        protected virtual void OnLogInfoReady (TitanicLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }

        #region IDisposable

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        protected virtual void Dispose (bool disposing)
        {
            if (disposing)
            {

            }
            // get rid of unmanaged resources
        }

        #endregion IDisposable
    }
}
