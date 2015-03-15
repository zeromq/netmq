using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MajordomoProtocol;
using MajordomoProtocol.Contracts;

using NetMQ;
using NetMQ.Sockets;
using NetMQ.zmq;

using TitanicCommons;

namespace TitanicProtocol
{
    /// <summary>
    ///     Wraps the MDP Broker with a layer of TITANIC which does the following
    ///     + writes messages to disc to ensure that none gets lost
    ///     + good for sporadically connecting clients/workers
    ///     + it uses the Majordomo Protocol
    /// 
    ///     it implements this broker asynchronous and handles all administrative work
    ///     if Run is called it automatically will Connect to the endpoint given
    ///     it however allows to alter that endpoint via Bind
    ///     it registers any worker with its service
    ///     it routes requests from clients to waiting workers offering the service the client has requested
    ///     as soon as they become available
    /// 
    ///     every client communicates with Titanic and sends requests or a request for a reply
    ///     Titanic answers with either a GUID identifying the request or with a reply for a request
    ///     according to the transfered GUID
    ///     Titanic in turn handles the communication with the broker transparently for the client
    ///     Titanic is organized in three different services (threads)
    ///         titanic.request -> storing the request and returning an GUID
    ///         titanic.reply   -> fetching a reply if one exists for an GUID and returning it
    ///         titanic.close   -> confirming that a reply has been stored and processed
    /// 
    ///     every request is answered with a GUID by Titanic to the client and if a client asks for the result
    ///     of his request he must send this GUID to identify the respective request/answer
    /// 
    ///     Services can/must be requested with a request, a.k.a. data to process
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
        private const string _TITANIC_DIR = ".titanic";
        private const string _TITANIC_QUEUE = "titanic.queue";
        private const string _TITANIC_ADDRESS = "tcp://localhost:5555";
        private const string _TITANIC_INTERNAL_COMMUNICATION = "inproc://titanic.inproc";

        /// <summary>
        ///     represents the application directory
        /// </summary>
        private readonly string m_appDir;
        /// <summary>
        ///     
        /// </summary>
        private readonly string m_titanicQueue;

        /// <summary>
        ///     if broker has a log message available if fires this event
        /// </summary>
        public event EventHandler<LogInfoEventArgs> LogInfoReady;


        public TitanicBroker ()
        {
            m_appDir = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _TITANIC_DIR);
            m_titanicQueue = Path.Combine (_TITANIC_DIR, _TITANIC_QUEUE);
        }

        /// <summary>
        ///     is the main thread of the broker
        ///     it spawns three threads handling request, reply and close commands
        ///     it receives GUID from Titanic Request Service
        ///     and dispatches the requests to workers
        /// </summary>
        public void Run ()
        {
            if (!Directory.Exists (_TITANIC_DIR))
                Directory.CreateDirectory (_TITANIC_DIR);

            if (!File.Exists (m_titanicQueue))
                File.Create (m_titanicQueue);

            using (var ctx = NetMQContext.Create ())
            using (var pipeStart = ctx.CreatePairSocket ())
            using (var pipeEnd = ctx.CreatePairSocket ())
            using (var cts = new CancellationTokenSource ())
            {
                // set up the inter thread communication pipe
                pipeStart.Bind (_TITANIC_INTERNAL_COMMUNICATION);
                pipeEnd.Connect (_TITANIC_INTERNAL_COMMUNICATION);

                // start the three child tasks
                var requestTask = Task.Factory.StartNew (() => ProcessTitanicRequest (pipeEnd), cts.Token);
                var replyTask = Task.Factory.StartNew (ProcessTitanicReply, cts.Token);
                var closeTask = Task.Factory.StartNew (ProcessTitanicClose, cts.Token);

                while (true)
                {
                    var input = pipeStart.Poll (PollEvents.PollIn, TimeSpan.FromMilliseconds (1000));

                    // any message available? -> process it
                    if ((input & PollEvents.PollIn) == PollEvents.PollIn)
                    {
                        var msg = pipeEnd.ReceiveString ();

                        Guid guid;
                        if (!Guid.TryParse (msg, out guid))
                            Log ("ERROR: received a malformed GUID - throw it away");
                        else
                        {
                            // now we have a valid GUID
                            // save it to disk for further use
                            TitanicIO.WriteNewRequest (m_titanicQueue, guid);
                        }
                    }

                    //! now dispatch (brute force) the requests -> SHOULD BE MORE INTELLIGENT (!)
                    foreach (var entry in TitanicIO.ReadNextNonProcessedRequest (m_titanicQueue))
                        if (DispatchRequests (entry.RequestId))
                            TitanicIO.WriteProcessedRequest (m_titanicQueue, entry);
                }
            }
        }

        /// <summary>
        ///     process any titanic request
        ///     
        ///     write request to disk and return the GUID to client
        ///     sends the GUID of the request back via the pipe
        ///     it connects to the PAIR socket to main thread
        /// </summary>
        private void ProcessTitanicRequest (PairSocket pipe)
        {
            // get a MDP worker with an automatic id and register with the service "titanic.request"
            // the worker will automatically start and connect to the indicated address
            using (IMDPWorker worker = new MDPWorker (_TITANIC_ADDRESS, TitanicOperation.Request.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a null, since there is no reply yet
                    var request = worker.Receive (reply);

                    // has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    if (!Directory.Exists (m_appDir))
                        Directory.CreateDirectory (m_appDir); // todo: add access security

                    var requestId = Guid.NewGuid ();
                    var filename = GetRequestFileName (requestId);
                    // save request to file -> [service][data]
                    // and filename == guid (!)
                    TitanicIO.WriteMessageToFile (filename, request);

                    // send GUID through message queue to main thread
                    pipe.Send (requestId.ToString ());

                    // return GUID via reply message via worker.Receive call
                    reply = new NetMQMessage ();
                    reply.Push (TitanicCommand.Ok.ToString ());
                    reply.Push (requestId.ToString ());
                }
            }
        }

        /// <summary>
        ///     process any titanic reply request
        ///     
        ///     will send an OK, PENDING or UNKNOWN as result of the request for the reply
        /// </summary>
        private void ProcessTitanicReply ()
        {
            // get a MDP worker with an automatic id and register with the service "titanic.reply"
            // the worker will automatically start and connect to the indicated address
            using (IMDPWorker worker = new MDPWorker (_TITANIC_ADDRESS, TitanicOperation.Reply.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a null, since there is no reply yet
                    var request = worker.Receive (reply);

                    // has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    if (!Directory.Exists (m_appDir))
                    {
                        // we have an out of sequence call - result requested before the request is registered!
                        reply = new NetMQMessage ();
                        reply.Push (TitanicCommand.Unknown.ToString ());
                    }
                    else
                    {
                        var requestIdAsString = request.Pop ().ConvertToString ();
                        var requestId = Guid.Parse (requestIdAsString);
                        var replyFilename = GetReplyFileName (requestId);

                        if (File.Exists (replyFilename))
                        {
                            reply = TitanicIO.ReadMessageFromFile (replyFilename);
                            reply.Push (TitanicCommand.Ok.ToString ());
                        }
                        else
                        {
                            reply = new NetMQMessage ();
                            var requestFilename = GetRequestFileName (requestId);

                            var replyCommand = (File.Exists (requestFilename)
                                                    ? TitanicCommand.Pending
                                                    : TitanicCommand.Unknown);

                            reply.Push (replyCommand.ToString ());
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     an idempotent method processing all requests to close a request with a GUID
        ///     it is safe to call it multiple times with the same GUID
        /// </summary>
        public void ProcessTitanicClose ()
        {
            // get a MDP worker with an automatic id and register with the service "titanic.Close"
            // the worker will automatically start and connect to the indicated address
            using (IMDPWorker worker = new MDPWorker (_TITANIC_ADDRESS, TitanicOperation.Close.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a null, since there is no reply yet
                    var request = worker.Receive (reply);

                    // has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    var guidAsString = request.Pop ().ConvertToString ();
                    var guid = Guid.Parse (guidAsString);
                    var requestFilename = GetRequestFileName (guid);
                    var replyFilename = GetReplyFileName (guid);

                    File.Delete (requestFilename);
                    File.Delete (replyFilename);

                    reply = new NetMQMessage ();
                    reply.Push (TitanicCommand.Ok.ToString ());
                }
            }
        }

        /// <summary>
        ///     dispatch the request with the specified GUID th the next available worker if any
        /// </summary>
        /// <param name="requestId">request's GUID</param>
        /// <returns><c>true</c> if successfull <c>false</c> otherwise</returns>
        private bool DispatchRequests (Guid requestId)
        {
            var requestFilename = GetRequestFileName (requestId);

            // is the request already been processed? -> file does not exist
            // threat this as successfully processed
            if (!File.Exists (requestFilename))
            {
                Log (string.Format ("Request file {0} does not exist.", requestFilename));

                return true;
            }

            // load request from file
            var request = TitanicIO.ReadMessageFromFile (requestFilename);
            // [service] is first frame and is a string
            var serviceName = request[0].ConvertToString ();

            Log (string.Format ("Do a ServiceCall for {0} - {1}.", serviceName, request));

            var reply = ServiceCall (serviceName, request);

            if (reply == null)
                return false;       // no reply

            // a reply has been received -> save it
            var replyFilename = GetRequestFileName (requestId);

            Log (string.Format ("Saving reply to {0}.", replyFilename));

            TitanicIO.WriteMessageToFile (replyFilename, reply);

            return true;
        }

        /// <summary>
        ///     carry out the actual call to the worker in order to process the request
        /// </summary>
        /// <param name="serviceName">the service's name requested</param>
        /// <param name="request">request to process by worker</param>
        /// <returns><c>true</c> if successfull and <c>false</c> otherwise</returns>
        private NetMQMessage ServiceCall (string serviceName, NetMQMessage request)
        {
            // create MDPClient session
            using (var session = new MDPClient (_TITANIC_ADDRESS))
            {
                session.Timeout = TimeSpan.FromMilliseconds (1000);     // 1s
                session.Retries = 1;                                    // only 1 retry
                // use MMI protocol to check if service is available
                var mmi = new NetMQMessage ();
                // add nam of service to inquire
                mmi.Append (serviceName);
                // request mmi.service resolution
                var reply = session.Send ("mmi.service", mmi);
                // first frame should be result of inquiry
                var answer = reply[0].ConvertToString ();
                // answer == "200" -> service is available -> make the request
                if (answer == "200")
                {
                    Log (string.Format ("ServiceCall -> {0} - {1}", serviceName, request));

                    return session.Send (serviceName, request);
                }

                Log (string.Format ("Service {0} is not available.", serviceName));

                return null;
            }
        }

        /// <summary>
        ///     return the full path + filename for a request
        /// </summary>
        private string GetRequestFileName (Guid guid)
        {
            return Path.Combine (m_appDir, guid + ".request");
        }

        /// <summary>
        ///     return the full path + filename for a reply
        /// </summary>
        private string GetReplyFileName (Guid guid)
        {
            return Path.Combine (m_appDir, guid + ".reply");
        }

        private void Log (string info)
        {
            if (string.IsNullOrWhiteSpace (info))
                return;

            OnLogInfoReady (new LogInfoEventArgs { Info = info });
        }

        protected virtual void OnLogInfoReady (LogInfoEventArgs e)
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
