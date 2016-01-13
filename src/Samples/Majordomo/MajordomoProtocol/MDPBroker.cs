using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using NetMQ;

using MDPCommons;
using NetMQ.Sockets;

namespace MajordomoProtocol
{
    /// <summary>
    ///     Implements a Broker according to Majordomo Protocol v0.1
    ///     it implements this broker asynchronous and handles all administrative work
    ///     if Run is called it automatically will Connect to the endpoint given
    ///     it however allows to alter that endpoint via Bind
    ///     it registers any worker with its service
    ///     it routes requests from clients to waiting workers offering the service the client has requested
    ///     as soon as they become available
    ///     also allows service discovery by clients via a special request 'mmi.service'
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
    public class MDPBroker : IMDPBroker
    {
        // broker expects from
        //      CLIENT  ->  [sender adr][e][protocol header][service name][request]
        //                  [sender adr][e][protocol header]['mmi.service'][service name]
        //      WORKER  ->  [sender adr][e][protocol header][mdp command][reply]

        // send from broker to
        //      CLIENT  ->  [CLIENT ADR][e][protocol header][SERVICENAME][DATA]
        //                  [CLIENT ADR][e][protocol header][RETURN CODE]
        //      WORKER  ->  READY       [WORKER ADR][e][protocol header][mdp command][service name]
        //                  REPLY       [WORKER ADR][e][protocol header][mdp command][client adr][e][reply]
        //                  HEARTBEAT   [WORKER ADR][e][protocol header][mdp command]
        // according to MDP the protocol header of a request must have the first frame with
        // a string stating:
        //      "MDP" for the protocol
        //      "C" for Client
        //   OR
        //      "W" for Worker
        //      "01" for the version of the Majordomo Protocol V0.1
        public static readonly string MDPClientHeader = "MDPC01";
        public static readonly string MDPWorkerHeader = "MDPW01";

        private readonly List<Service> m_services;              // list of known services
        private readonly List<Worker> m_knownWorkers;           // list of all known workers, regardless of service offered

        private string m_endpoint;                              // the endpoint the broker binds to
        private int m_heartbeatLiveliness;                      // indicates the 'liveliness' of a worker
        private TimeSpan m_heartbeatExpiry;                     // when a worker expires -> set by HeartbeatLiveliness(!)
        private readonly TimeSpan m_heartbeatInterval;          // the time interval between heartbeats
        private bool m_isBound;                                 // true if socket is bound to address
        private bool m_isRunning;                               // true if the broker is running

        private readonly object m_syncRoot = new object ();     // used as synchronization object for Purge ()

        /// <summary>
        ///     the socket for communicating with clients and workers
        /// </summary>
        public NetMQSocket Socket { get; private set; }

        /// <summary>
        ///     the interval at which the broker send heartbeats to workers
        ///     initially set to 2.500 ms
        /// </summary>
        public TimeSpan HeartbeatInterval { get { return m_heartbeatInterval; } }

        /// <summary>
        ///     after so many heartbeat cycles a worker is deemed to dead
        ///     initially set to 3 max. 5 is reasonable
        /// </summary>
        public int HeartbeatLiveliness
        {
            get { return m_heartbeatLiveliness; }
            set
            {
                m_heartbeatLiveliness = value;
                m_heartbeatExpiry = TimeSpan.FromMilliseconds (HeartbeatInterval.TotalMilliseconds * value);
            }
        }

        /// <summary>
        ///     if broker has a log message available if fires this event
        /// </summary>
        public event EventHandler<MDPLogEventArgs> LogInfoReady;

        /// <summary>
        ///     broadcast elaborate debugging info
        /// </summary>
        public event EventHandler<MDPLogEventArgs> DebugInfoReady;

        /// <summary>
        ///     ctor initializing all local variables
        ///     <para>DEFAULTS:</para>
        ///     <para>heartbeat interval 2,500ms</para>
        ///     <para>max retries for waiting on heartbeats 3 times</para>
        /// </summary>
        private MDPBroker ()
        {
            Socket = new RouterSocket ();
            m_services = new List<Service> ();
            m_knownWorkers = new List<Worker> ();
            m_heartbeatInterval = TimeSpan.FromMilliseconds (2500); // otherwise the expiry would be 0(!)
            HeartbeatLiveliness = 3;                    // so m_heartbeatExpiry = value * m_heartbeatInterval = 7.500 ms
            m_isBound = false;
        }

        /// <summary>
        ///     ctor initializing all private variables and set the user requested once
        /// </summary>
        /// <param name="endpoint">a valid NetMQ endpoint for the broker</param>
        /// <param name="heartbeatInterval">the interval between heartbeats in milliseconds, default 2,500ms</param>
        public MDPBroker ([NotNull] string endpoint, int heartbeatInterval = 0)
            : this ()
        {
            if (string.IsNullOrWhiteSpace (endpoint))
                throw new ArgumentNullException ("endpoint", "An 'endpoint' were the broker binds to must be given!");

            if (heartbeatInterval > 0)
                m_heartbeatInterval = TimeSpan.FromMilliseconds (heartbeatInterval);

            m_endpoint = endpoint;
        }

        /// <summary>
        ///     broker binds his socket to this endpoint upon request
        /// </summary>
        /// <exception cref="ApplicationException">The bind operation failed. Most likely because 'endpoint' is malformed!</exception>
        /// <remarks>
        ///     broker uses the same endpoint to communicate with clients and workers(!)
        /// </remarks>
        public void Bind ()
        {
            if (m_isBound)
                return;

            try
            {
                Socket.Bind (m_endpoint);
            }
            catch (Exception e)
            {
                var error = string.Format ("The bind operation failed. Most likely because 'endpoint' ({0}) is malformed!",
                                           m_endpoint);
                error += string.Format ("\nMessage: {0}", e.Message);
                throw new ApplicationException (error);
            }

            m_isBound = true;

            var major = Assembly.GetExecutingAssembly ().GetName ().Version.Major;
            var minor = Assembly.GetExecutingAssembly ().GetName ().Version.Minor;

            Log (string.Format ("MDP Broker/{0}.{1} is active at {2}", major, minor, m_endpoint));
        }

        /// <summary>
        ///     allows to change the address the broker binds to, broker must not operate
        /// </summary>
        /// <param name="endpoint">the new endpoint to bind to</param>
        /// <exception cref="InvalidOperationException">Can not change binding while operating!</exception>
        public void Bind ([NotNull] string endpoint)
        {
            if (m_isRunning)
                throw new InvalidOperationException ("Can not change binding while operating!");

            if (m_endpoint == endpoint && m_isBound)
                return;

            if (m_isBound)
                Socket.Unbind (m_endpoint);

            m_endpoint = endpoint;

            Bind ();
        }

        /// <summary>
        ///     run the broker - if not bound to endpoint automatically binds to known endpoint
        /// </summary>
        /// <param name="token">CancellationToken to cancel the method</param>
        /// <exception cref="InvalidOperationException">Can't start same broker more than once!</exception>
        public async Task Run (CancellationToken token)
        {
            if (m_isRunning)
                throw new InvalidOperationException ("Can't start same broker more than once!");

            if (!m_isBound)
                Bind ();

            m_isRunning = true;

            using (var poller = new NetMQPoller ())
            {
                Socket.ReceiveReady += ProcessReceivedMessage;
                // get timer for scheduling heartbeat
                var timer = new NetMQTimer (HeartbeatInterval);
                // send every 'HeartbeatInterval' a heartbeat to all not expired workers
                timer.Elapsed += (s, e) => SendHeartbeat ();

                poller.Add (Socket);
                poller.Add (timer);

                Log ("Starting to listen for incoming messages ...");

                // start the poller and wait for the return, which will happen once token is
                // signalling Cancel(!)
                await Task.Factory.StartNew (poller.Run, token);

                Log ("... Stopped!");

                // clean up
                poller.Remove (timer);
                poller.Remove (Socket);
                // unregister event handler
                Socket.ReceiveReady -= ProcessReceivedMessage;
            }

            m_isRunning = false;
        }

        /// <summary>
        ///     run the broker - if not bound to endpoint automatically binds to known endpoint
        /// </summary>
        /// <param name="token">CancellationToken to cancel the method</param>
        /// <exception cref="InvalidOperationException">Can't start same broker more than once!</exception>
        public void RunSynchronous (CancellationToken token)
        {
            if (m_isRunning)
                throw new InvalidOperationException ("Can't start same broker more than once!");

            if (!m_isBound)
                Bind ();

            m_isRunning = true;

            using (var poller = new NetMQPoller ())
            {
                Socket.ReceiveReady += ProcessReceivedMessage;
                // get timer for scheduling heartbeat
                var timer = new NetMQTimer (HeartbeatInterval);
                // send every 'HeartbeatInterval' a heartbeat to all not expired workers
                timer.Elapsed += (s, e) => SendHeartbeat ();

                poller.Add (Socket);
                poller.Add (timer);

                Log ("Starting to listen for incoming messages ...");

                // start the poller and wait for the return, which will happen once token is
                // signalling Cancel(!)
                Task.Factory.StartNew (poller.Run, token).Wait ();

                Log ("... Stopped!");

                // clean up
                poller.Remove (timer);
                poller.Remove (Socket);
                // unregister event handler
                Socket.ReceiveReady -= ProcessReceivedMessage;
            }

            m_isRunning = false;
        }

        /// <summary>
        ///     send a heartbeat to all known active worker
        /// </summary>
        public void SendHeartbeat ()
        {
            Purge ();

            foreach (var worker in m_knownWorkers)
                WorkerSend (worker, MDPCommand.Heartbeat, null);

            DebugLog ("Sent HEARTBEAT to all worker!");
        }

        /// <summary>
        ///     expect from
        ///     CLIENT  ->  [sender adr][e][protocol header][service name][request]
        ///     WORKER  ->  [sender adr][e][protocol header][mdp command][reply]
        /// </summary>
        private void ProcessReceivedMessage (object sender, NetMQSocketEventArgs e)
        {
            var msg = e.Socket.ReceiveMultipartMessage ();

            DebugLog (string.Format ("Received: {0}", msg));

            var senderFrame = msg.Pop ();               // [e][protocol header][service or command][data]
            var empty = msg.Pop ();                     // [protocol header][service or command][data]
            var headerFrame = msg.Pop ();               // [service or command][data]
            var header = headerFrame.ConvertToString ();

            if (header == MDPClientHeader)
                ProcessClientMessage (senderFrame, msg);
            else
                if (header == MDPWorkerHeader)
                    ProcessWorkerMessage (senderFrame, msg);
                else
                    Log (string.Format ("ERROR - message with invalid protocol header!"));
        }

        /// <summary>
        ///     process a READY, REPLY, HEARTBEAT, DISCONNECT message sent to the broker by a worker
        /// </summary>
        /// <param name="sender">the sender identity frame</param>
        /// <param name="message">the message sent</param>
        public void ProcessWorkerMessage ([NotNull] NetMQFrame sender, [NotNull] NetMQMessage message)
        {
            // should be
            // READY        [mdp command][service name]
            // REPLY        [mdp command][client adr][e][reply]
            // HEARTBEAT    [mdp command]
            if (message.FrameCount < 1)
                throw new ApplicationException ("Message with too few frames received.");

            var mdpCommand = message.Pop ();                // get the mdp command frame it should have only one byte payload

            if (mdpCommand.BufferSize > 1)
                throw new ApplicationException ("The MDPCommand frame had more than one byte!");

            var cmd = (MDPCommand) mdpCommand.Buffer[0];    // [service name] or [client adr][e][reply]
            var workerId = sender.ConvertToString ();       // get the id of the worker sending the message
            var workerIsKnown = m_knownWorkers.Any (w => w.Id == workerId);

            switch (cmd)
            {
                case MDPCommand.Ready:
                    if (workerIsKnown)
                    {
                        // then it is not the first command in session -> WRONG
                        // if the worker is know to a service, remove it from that
                        // service and a potential waiting list therein
                        RemoveWorker (m_knownWorkers.Find (w => w.Id == workerId));

                        Log (string.Format ("READY out of sync. Removed worker {0}.", workerId));
                    }
                    else
                    {
                        // now a new - not know - worker sent his READY initiation message
                        // attach worker to service and mark as idle - worker has send READY as first message
                        var serviceName = message.Pop ().ConvertToString ();
                        var service = ServiceRequired (serviceName);
                        // create a new worker and set the service it belongs to
                        var worker = new Worker (workerId, sender, service);
                        // now add the worker
                        AddWorker (worker, service);

                        Log (string.Format ("READY processed. Worker {0} added to service {1}",
                                            workerId,
                                            serviceName));
                    }
                    break;
                case MDPCommand.Reply:
                    if (workerIsKnown)
                    {
                        var worker = m_knownWorkers.Find (w => w.Id == workerId);
                        // remove the client return envelope and insert the protocol header
                        // and service name then rewrap the envelope
                        var client = UnWrap (message);                  // [reply]
                        message.Push (worker.Service.Name);             // [service name][reply]
                        message.Push (MDPClientHeader);                 // [protocol header][service name][reply]
                        var reply = Wrap (client, message);             // [client adr][e][protocol header][service name][reply]

                        Socket.SendMultipartMessage (reply);

                        DebugLog (string.Format ("REPLY from {0} received and send to {1} -> {2}",
                                            workerId,
                                            client.ConvertToString (),
                                            message));
                        // relist worker for further requests
                        AddWorker (worker, worker.Service);
                    }
                    break;
                case MDPCommand.Heartbeat:
                    if (workerIsKnown)
                    {
                        var worker = m_knownWorkers.Find (w => w.Id == workerId);
                        worker.Expiry = DateTime.UtcNow + m_heartbeatExpiry;

                        DebugLog (string.Format ("HEARTBEAT from {0} received.", workerId));
                    }
                    break;
                default:
                    Log ("ERROR: Invalid MDPCommand received or message received!");
                    break;
            }
        }

        /// <summary>
        ///     process REQUEST from a client. MMI requests are implemented here directly
        /// </summary>
        /// <param name="sender">client identity</param>
        /// <param name="message">the message received</param>
        public void ProcessClientMessage ([NotNull] NetMQFrame sender, [NotNull] NetMQMessage message)
        {
            // should be
            // REQUEST      [service name][request] OR
            // DISCOVER     ['mmi.service'][service to discover]
            if (message.FrameCount < 2)
                throw new ArgumentException ("The message is malformed!");

            var serviceFrame = message.Pop ();                 // [request] OR [service to discover]
            var serviceName = serviceFrame.ConvertToString ();
            var request = Wrap (sender, message);              // [CLIENT ADR][e][request] OR [service name]

            // if it is a "mmi.service" request, handle it locally
            // this request searches for a service and returns a code indicating the result of that search
            // OK       =>   service exists and worker are available
            // Pending  =>   service exists but no workers are available
            // Unknown  =>   service does not exist
            if (serviceName == "mmi.service")
            {
                var returnCode = MmiCode.Unknown;
                var name = request.Last.ConvertToString ();

                if (m_services.Exists (s => s.Name == name))
                {
                    var svc = m_services.Find (s => s.Name == name);

                    returnCode = svc.DoWorkersExist () ? MmiCode.Ok : MmiCode.Pending;
                }
                // set the return code to be the last frame in the message
                var rc = new NetMQFrame (returnCode.ToString ());// [return code]

                request.RemoveFrame (message.Last);             // [CLIENT ADR][e] -> [service name]
                request.Append (serviceName);                   // [CLIENT ADR][e] <- ['mmi.service']
                request.Append (rc);                            // [CLIENT ADR][e]['mmi.service'] <- [return code]

                // remove client return envelope and insert
                // protocol header and service name,
                // then rewrap envelope
                var client = UnWrap (request);                  // ['mmi.service'][return code]
                request.Push (MDPClientHeader);                 // [protocol header]['mmi.service'][return code]
                var reply = Wrap (client, request);             // [CLIENT ADR][e][protocol header]['mmi.service'][return code]

                // send to back to CLIENT(!)
                Socket.SendMultipartMessage (reply);

                DebugLog (string.Format ("MMI request processed. Answered {0}", reply));
            }
            else
            {
                // get the requested service object
                var service = ServiceRequired (serviceName);

                // a standard REQUEST received
                DebugLog (string.Format ("Dispatching -> {0} to {1}", request, serviceName));

                // send to a worker offering the requested service
                // will add command, header and worker adr envelope
                ServiceDispatch (service, request);             // [CLIENT ADR][e][request]
            }
        }

        /// <summary>
        ///     This method deletes any idle workers that haven't pinged us in a
        ///     while. We hold workers from oldest to most recent so we can stop
        ///     scanning whenever we find a live worker. This means we'll mainly stop
        ///     at the first worker, which is essential when we have large numbers of
        ///     workers (we call this method in our critical path)
        /// </summary>
        /// <remarks>
        ///     we must use a lock to guarantee that only one thread will have
        ///     access to the purging at a time!
        /// </remarks>
        private void Purge ()
        {
            DebugLog ("start purging for all services");

            lock (m_syncRoot)
            {
                foreach (var service in m_services)
                {
                    foreach (var worker in service.WaitingWorkers)
                    {
                        if (DateTime.UtcNow < worker.Expiry)
                            // we found the first woker not expired in that service
                            // any following worker will be younger -> we're done for the service
                            break;

                        RemoveWorker (worker);
                    }
                }
            }
        }

        /// <summary>
        ///     adds the worker to the service and the known worker list
        ///     if not already known
        ///     it dispatches pending requests for this service as well
        /// </summary>
        private void AddWorker (Worker worker, Service service)
        {
            worker.Expiry = DateTime.UtcNow + m_heartbeatExpiry;

            if (!m_knownWorkers.Contains (worker))
            {
                m_knownWorkers.Add (worker);

                DebugLog (string.Format ("added {0} to known worker.", worker.Id));
            }

            service.AddWaitingWorker (worker);

            DebugLog (string.Format ("added {0} to waiting worker in service {1}.",
                                     worker.Id,
                                     service.Name));

            // get pending messages out
            ServiceDispatch (service, null);
        }

        /// <summary>
        ///     removes the worker from the known worker list and
        ///     from the service it refers to if it exists and
        ///     potentially from the waiting list therein
        /// </summary>
        /// <param name="worker"></param>
        private void RemoveWorker (Worker worker)
        {
            if (m_services.Contains (worker.Service))
            {
                var service = m_services.Find (s => s.Equals (worker.Service));
                service.DeleteWorker (worker);

                DebugLog (string.Format ("removed worker {0} from service {1}",
                                         worker.Id,
                                         service.Name));
            }

            m_knownWorkers.Remove (worker);

            DebugLog (string.Format ("removed {0} from known worker.", worker.Id));
        }

        /// <summary>
        ///     sends a message to a specific worker with a specific command
        ///     and add an option option
        /// </summary>
        private void WorkerSend (Worker worker, MDPCommand command, NetMQMessage message, string option = null)
        {
            var msg = message ?? new NetMQMessage ();

            // stack protocol envelope to start of message
            if (!ReferenceEquals (option, null))
                msg.Push (option);

            msg.Push (new[] { (byte) command });
            msg.Push (MDPWorkerHeader);
            // stack routing envelope
            var request = Wrap (worker.Identity, msg);

            DebugLog (string.Format ("Sending {0}", request));
            // send to worker
            Socket.SendMultipartMessage (request);
        }

        /// <summary>
        ///     locates service by name or creates new service if there
        ///     is no service available with that name
        /// </summary>
        /// <param name="serviceName">the service requested</param>
        /// <returns>the requested service object</returns>
        private Service ServiceRequired (string serviceName)
        {
            var svc = m_services.Exists (s => s.Name == serviceName)
                          ? m_services.Find (s => s.Name == serviceName)
                          : new Service (serviceName);
            // add service to the known services
            m_services.Add (svc);

            return svc;
        }

        /// <summary>
        ///     sends as much pending requests as there are waiting workers
        ///     of the specified service by
        ///     a) add the request to the pending requests within this service
        ///        if there is a message
        ///     b) remove all expired workers of this service
        ///     c) while there are waiting workers
        ///             get the next pending request
        ///             send to the worker
        /// </summary>
        private void ServiceDispatch (Service service, NetMQMessage message)
        {
            DebugLog (string.Format ("Service [{0}] dispatches -> {1}",
                                     service.Name,
                                     message == null ? "PURGING" : "message = " + message));

            // if message is 'null' just send pending requests
            if (!ReferenceEquals (message, null))
                service.AddRequest (message);

            // remove all expired workers!
            Purge ();

            // if there are requests pending and workers are waiting dispatch the requests
            // as long as there are workers and requests
            while (service.CanDispatchRequests ())
            {
                var worker = service.GetNextWorker ();

                if (service.PendingRequests.Count > 0)
                {
                    var request = service.GetNextRequest ();

                    DebugLog (string.Format ("Service Dispatch -> pending request {0} to {1}", request, worker.Id));

                    WorkerSend (worker, MDPCommand.Request, request);
                }
                else
                    // no more requests -> we're done
                    break;
            }
        }

        protected virtual void OnLogInfoReady (MDPLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }

        protected virtual void OnDebugInfoReady (MDPLogEventArgs e)
        {
            var handler = DebugInfoReady;

            if (handler != null)
                handler (this, e);
        }

        private void Log (string info)
        {
            if (string.IsNullOrWhiteSpace (info))
                return;

            OnLogInfoReady (new MDPLogEventArgs { Info = "[MDP BROKER] " + info });
        }

        private void DebugLog (string info)
        {
            if (string.IsNullOrWhiteSpace (info))
                return;

            OnDebugInfoReady (new MDPLogEventArgs { Info = "[MDP BROKER DEBUG] " + info });
        }

        /// <summary>
        ///     returns the first frame and deletes the next frame if it is empty
        ///
        ///     CHANGES message!
        /// </summary>
        private static NetMQFrame UnWrap (NetMQMessage message)
        {
            var frame = message.Pop ();

            if (message.First == NetMQFrame.Empty)
                message.Pop ();

            return frame;
        }

        /// <summary>
        ///     adds an empty frame and a specified frame to a message
        /// </summary>
        private static NetMQMessage Wrap (NetMQFrame frame, NetMQMessage message)
        {
            var result = new NetMQMessage (message);

            if (frame.BufferSize > 0)
            {
                result.Push (NetMQFrame.Empty);
                result.Push (frame);
            }

            return result;
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
                Socket.Dispose ();
            }
            // get rid of unmanaged resources
        }

        #endregion IDisposable
    }
}
