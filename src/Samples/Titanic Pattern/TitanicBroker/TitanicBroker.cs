using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

using MajordomoProtocol;
using MajordomoProtocol.Contracts;

using NetMQ;
using NetMQ.Sockets;
using NetMQ.zmq;

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
        private const string _TITANIC_INTERNAL_COMMUNICATION = "inproc://titanic.inproc";

        private readonly string m_titanicAddress;

        private readonly TitanicIO titanicIO;

        /// <summary>
        ///     if broker has a log message available if fires this event
        /// </summary>
        public event EventHandler<LogInfoEventArgs> LogInfoReady;

        /// <summary>
        ///     ctor - initializes the broker ip to "tcp://localhost:5555"
        /// </summary>
        public TitanicBroker ()
        {
            m_titanicAddress = "tcp://localhost:5555";
            titanicIO = new TitanicIO ();
        }

        /// <summary>
        ///     ctor with broker ip
        /// </summary>
        /// <param name="brokerIP">the ip address of the broker as string</param>
        /// <exception cref="ApplicationException">The broker ip address is invalid!</exception>
        public TitanicBroker ([NotNull] string brokerIP)
            : this ()
        {
            IPAddress ip;
            if (!IPAddress.TryParse (brokerIP, out ip))
                throw new ApplicationException ("The IP address of the broker is invalid!");

            m_titanicAddress = brokerIP;
        }

        /// <summary>
        ///     <para>is the main thread of the broker</para>
        /// 
        ///     <para>it spawns three threads handling request, reply and close commands</para>
        ///     it receives GUID from Titanic Request Service and dispatches the requests to workers
        /// </summary>
        public void Run ()
        {
            using (var ctx = NetMQContext.Create ())
            using (var pipeStart = ctx.CreatePairSocket ())
            using (var pipeEnd = ctx.CreatePairSocket ())
            using (var cts = new CancellationTokenSource ())
            {
                // set up the inter thread communication pipe
                pipeStart.Bind (_TITANIC_INTERNAL_COMMUNICATION);
                pipeEnd.Connect (_TITANIC_INTERNAL_COMMUNICATION);

                // start the three child tasks
                var requestTask = Task.Run (() => ProcessTitanicRequest (pipeEnd), cts.Token);
                var replyTask = Task.Run (() => ProcessTitanicReply (), cts.Token);
                var closeTask = Task.Run (() => ProcessTitanicClose (), cts.Token);

                var tasks = new[] { requestTask, replyTask, closeTask };

                while (true)
                {
                    var input = pipeStart.Poll (PollEvents.PollIn, TimeSpan.FromMilliseconds (1000));

                    // any message available? -> process it
                    if ((input & PollEvents.PollIn) == PollEvents.PollIn)
                    {
                        // only one frame will be send [Guid]
                        var msg = pipeStart.ReceiveFrameString ();

                        Guid guid;
                        if (!Guid.TryParse (msg, out guid))
                            Log ("Received a malformed GUID - throw it away");
                        else
                        {
                            // now we have a valid GUID - save it to disk for further use
                            titanicIO.SaveNewRequest (guid);
                        }
                    }

                    //! now dispatch (brute force) the requests -> SHOULD BE MORE INTELLIGENT (!)
                    foreach (var entry in titanicIO.RetrieveRequests ().Where (entry => DispatchRequests (entry.RequestId)))
                        titanicIO.SaveProcessedRequest (entry);

                    //! should implement some sort of restart
                    // beware of the silently dieing threads - must be detected!
                    if (DidAnyTaskStopp (tasks))
                    {
                        // stopp all threads
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
        private void ProcessTitanicRequest ([NotNull] PairSocket pipe)
        {
            // get a MDP worker with an automatic id and register with the service "titanic.request"
            // the worker will automatically start and connect to a MDP Broker at the indicated address
            using (IMDPWorker worker = new MDPWorker (m_titanicAddress, TitanicOperation.Request.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a 'null', since there is no initial reply
                    // should be [service name][request data]
                    var request = worker.Receive (reply);

                    // has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    // generate Guid for the request
                    var requestId = Guid.NewGuid ();
                    // save request to file -> [service name][request data]
                    titanicIO.SaveMessage (TitanicOperation.Request, requestId, request);
                    // send GUID through message queue to main thread
                    pipe.Send (requestId.ToString ());
                    // return GUID via reply message via worker.Receive call
                    reply = new NetMQMessage ();
                    // [Ok]
                    reply.Push (TitanicCommand.Ok.ToString ());
                    // [Ok][Guid]
                    reply.Push (requestId.ToString ());
                }
            }
        }

        /// <summary>
        ///     process any titanic reply request by a client
        ///     
        ///     <para>will send an OK, PENDING or UNKNOWN as result of the request for the reply</para>
        /// </summary>
        private void ProcessTitanicReply ()
        {
            // get a MDP worker with an automatic id and register with the service "titanic.reply"
            // the worker will automatically start and connect to the indicated address
            using (IMDPWorker worker = new MDPWorker (m_titanicAddress, TitanicOperation.Reply.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication to MDP Broker with sending a 'null', 
                    // since there is no initial reply
                    var request = worker.Receive (reply);

                    // has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;

                    var requestIdAsString = request.Pop ().ConvertToString ();
                    var requestId = Guid.Parse (requestIdAsString);

                    if (titanicIO.Exists (TitanicOperation.Reply, requestId))
                    {
                        reply = titanicIO.RetrieveMessage (TitanicOperation.Reply, requestId);
                        reply.Push (TitanicCommand.Ok.ToString ());
                    }
                    else
                    {
                        reply = new NetMQMessage ();

                        var replyCommand = (titanicIO.Exists (TitanicOperation.Request, requestId)
                                                ? TitanicCommand.Pending
                                                : TitanicCommand.Unknown);

                        reply.Push (replyCommand.ToString ());
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
            // the worker will automatically start and connect to MDP Broker with the indicated address
            using (IMDPWorker worker = new MDPWorker (m_titanicAddress, TitanicOperation.Close.ToString ()))
            {
                NetMQMessage reply = null;

                while (true)
                {
                    // initiate the communication with sending a null, since there is no reply yet
                    var request = worker.Receive (reply);

                    // has there been a breaking cause? -> exit
                    if (ReferenceEquals (request, null))
                        break;
                    // we expect [Guid] as the only frame
                    var guidAsString = request.Pop ().ConvertToString ();
                    var guid = Guid.Parse (guidAsString);
                    // close the request
                    titanicIO.CloseRequest (guid);

                    // send back the confirmation
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
            // is the request already been processed? -> file does not exist
            // threat this as successfully processed
            if (!titanicIO.Exists (TitanicOperation.Request, requestId))
            {
                Log (string.Format ("Request {0} does not exist.", requestId));

                return true;
            }

            // load request from file
            var request = titanicIO.RetrieveMessage (TitanicOperation.Request, requestId);
            // [service] is first frame and is a string
            var serviceName = request[0].ConvertToString ();

            Log (string.Format ("Do a ServiceCall for {0} - {1}.", serviceName, request));

            var reply = ServiceCall (serviceName, request);

            if (reply == null)
                return false;       // no reply

            // a reply has been received -> save it
            Log (string.Format ("Saving reply for request {0}.", requestId));

            titanicIO.SaveMessage (TitanicOperation.Reply, requestId, reply);

            return true;
        }

        /// <summary>
        ///     carry out the actual call to the worker in order to process the request
        /// </summary>
        /// <param name="serviceName">the service's name requested</param>
        /// <param name="request">request to process by worker</param>
        /// <returns><c>true</c> if successfull and <c>false</c> otherwise</returns>
        private NetMQMessage ServiceCall ([NotNull] string serviceName, [NotNull] NetMQMessage request)
        {
            // create MDPClient session
            using (var session = new MDPClient (m_titanicAddress))
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
                Log (string.Format ("An expection has been detected! ABANDONING!"));
                // get the exceptions available and log them
                foreach (var task in tasks.Where (task => task.IsFaulted))
                    LogExceptions (task.Exception);

                return true;
            }

            return false;
        }

        /// <summary>
        ///     log all exceptions of the AggregateException
        /// </summary>
        private void LogExceptions (AggregateException exception)
        {
            Log (string.Format ("Exception: {0}", exception.Message));

            foreach (var ex in exception.Flatten ().InnerExceptions)
                Log (string.Format ("Inner Exception: {0}", ex.Message));
        }

        private void Log ([NotNull] string info)
        {
            if (string.IsNullOrWhiteSpace (info))
                return;

            OnLogInfoReady (new LogInfoEventArgs { Info = "[TITANIC] " + info });
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
