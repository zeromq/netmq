using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MajordomoProtocol.Contracts;

using NetMQ;

namespace MajordomoProtocol
{
    public class MDPBroker : IMDPBroker, IDisposable
    {
        private readonly NetMQContext m_ctx;

        private readonly string m_endpoint;                     // the endpoint the broker binds to
        private readonly List<Service> m_services;              // list of known services
        private readonly List<Worker> m_knownWorkers;           // list of all known workers, regardless of service offered 
        private TimeSpan m_heartbeatExpiry;
        private int m_heartbeatLiveliness;
        private TimeSpan m_heartbeatInterval;

        // according to MDP the protocol header of a request must have the first frame with
        // a string stating:
        //      "MDP" for the protocol
        //      "C" for Client
        //   OR
        //      "W" for Worker
        //      "01" for the version of the Majordomo Protocol V0.1
        public static readonly string MDPClientHeader = "MDPC01";
        public static readonly string MDPWorkerHeader = "MDPW01";

        /// <summary>
        ///     the socket for communicating with clients & workers
        /// </summary>
        public NetMQSocket Socket { get; private set; }

        /// <summary>
        ///     the interval at which the broker send hearbeats to workers
        ///     initially set to 2.500 ms
        /// </summary>
        public TimeSpan HeartbeatInterval
        {
            get { return m_heartbeatInterval; }
            set
            {
                m_heartbeatInterval = value;
                m_heartbeatExpiry = TimeSpan.FromMilliseconds (HeartbeatInterval.TotalMilliseconds * HeartbeatLiveliness);
            }
        }

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
                m_heartbeatExpiry = TimeSpan.FromMilliseconds (HeartbeatInterval.TotalMilliseconds * HeartbeatLiveliness);
            }
        }

        /// <summary>
        ///     if broker has a log message available if fires this event
        /// </summary>
        public event EventHandler<LogInfoEventArgs> LogInfoReady;

        /// <summary>
        ///     ctor initializing all local variables
        /// </summary>
        private MDPBroker ()
        {
            m_ctx = NetMQContext.Create ();
            Socket = m_ctx.CreateRouterSocket ();
            m_services = new List<Service> ();
            m_knownWorkers = new List<Worker> ();
            HeartbeatInterval = TimeSpan.FromMilliseconds (2500);
            HeartbeatLiveliness = 3;
            m_heartbeatExpiry = TimeSpan.FromMilliseconds (HeartbeatInterval.TotalMilliseconds * HeartbeatLiveliness);
        }

        /// <summary>
        ///     ctor initializing all private variables and set the user requested once
        /// </summary>
        /// <param name="endpoint">the endpoint the broker binds to</param>
        public MDPBroker (string endpoint)
            : this ()
        {
            if (string.IsNullOrWhiteSpace (endpoint))
                throw new ArgumentNullException ("endpoint", "An 'endpint' were the broker binds to must be given!");

            m_endpoint = endpoint;
        }

        /// <summary>
        ///     broker binds his socket to this endpoint
        /// </summary>
        /// <remarks>
        ///     broker uses the same endpoint to communicate with clients and workers(!)
        /// </remarks>
        public void Bind ()
        {
            Socket.Bind (m_endpoint);

            OnLogInfoReady (new LogInfoEventArgs
                            {
                                LogInfo = string.Format ("[BROKER] MDP Broker/0.2.0 is active at {0}", m_endpoint)
                            });
        }

        #region NOT FINISHED NOR TESTED
        // if that works than Socket can be made private and an User of a broker instance
        //  Bind could also be internalized
        // would only do the following
        //  <c>
        //
        //    var cts = new CancellationTokenSource ();
        //
        //    Console.CancelKeyPress += (s, e) =>
        //                              {
        //                                  e.Cancel = true;
        //                                  cts.Cancel ();
        //                              };
        //
        //      using (var broker = new MDPBroker (endpoint)
        //      {
        //         await broker.Run (cts.Token);
        //      }
        //  </c>

        /// <summary>
        ///     run the broker
        /// </summary>
        /// <param name="token">CancellationToken to cancel the method</param>
        public async Task Run (CancellationToken token)
        {
            Socket.ReceiveReady += ProcessReceiveMessage;
            var timer = new NetMQTimer (HeartbeatInterval);
            // send every 'HeartbeatInterval' a heartbeat to all not expired workers
            timer.Elapsed += ProcessTimeElapsed;

            using (var poller = new Poller ())
            {
                poller.AddSocket (Socket);
                poller.AddTimer (timer);
                Console.WriteLine ("starting to listen to incoming messages ...");

                await Task.Factory.StartNew (poller.Start, token);

                if (poller.IsStarted)
                    poller.Stop ();

                // clean up
                poller.RemoveTimer (timer);
                poller.RemoveSocket (Socket);
                // unregister event handler
                timer.Elapsed -= ProcessTimeElapsed;
                Socket.ReceiveReady -= ProcessReceiveMessage;
            }
        }

        /// <summary>
        ///     expect from
        ///     CLIENT  ->  [sender adr][e][protocol header][service name][request]
        ///     WORKER  ->  [sender adr][e][protocol header][mdp command][reply]
        /// </summary>
        public void ProcessReceiveMessage (object sender, NetMQSocketEventArgs e)
        {
            var msg = e.Socket.ReceiveMessage ();

            OnLogInfoReady (new LogInfoEventArgs { LogInfo = string.Format ("[BROKER PROCESS] Received {0}", msg) });

            var senderFrame = msg.Pop ();               // [e][protocol header][service or command][data]
            var empty = msg.Pop ();                     // [protocol header][service or command][data]
            var headerFrame = msg.Pop ();               // [service or command][data]
            var header = headerFrame.ConvertToString ();

            if (header == MDPBroker.MDPClientHeader)
                ProcessClientMessage (senderFrame, msg);
            else
                if (header == MDPBroker.MDPWorkerHeader)
                    ProcessWorkerMessage (senderFrame, msg);
                else
                    OnLogInfoReady (new LogInfoEventArgs
                                    {
                                        LogInfo = string.Format ("[BROKER PROCESS] ERROR - message with invalid protocol header!")
                                    });
        }

        public void ProcessTimeElapsed (object sender, NetMQTimerEventArgs e)
        {
            OnLogInfoReady (new LogInfoEventArgs { LogInfo = "[BROKER PROCESS] Sending heartbeat" });

            SendHeartbeat ();
        }

        #endregion

        /// <summary>
        ///     process a READY, REPLY, HEARTBEAT, DISCONNECT message sent to the broker by a worker
        /// </summary>
        /// <param name="sender">the sender identity frame</param>
        /// <param name="message">the message sent</param>
        public void ProcessWorkerMessage (NetMQFrame sender, NetMQMessage message)
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
                        Wrap (client, message);                         // [client adr][e][protocol header][service name][reply]

                        Socket.SendMessage (message);
                    }
                    break;
                case MDPCommand.Heartbeat:
                    if (workerIsKnown)
                    {
                        var worker = m_knownWorkers.Find (w => w.Id == workerId);
                        worker.Expiry = DateTime.UtcNow + m_heartbeatExpiry;
                    }
                    break;
                default:
                    OnLogInfoReady (new LogInfoEventArgs { LogInfo = "Invalid MDPCommand received or message received!" });
                    break;
            }
        }

        /// <summary>
        ///     process REQUESt from a client. MMI requests are implemented here directly
        /// </summary>
        /// <param name="sender">client identity</param>
        /// <param name="message">the message received</param>
        public void ProcessClientMessage (NetMQFrame sender, NetMQMessage message)
        {
            // we expect [SERVICENAME][DATA]
            if (message.FrameCount < 2)
                throw new ArgumentException ("The message is malformed!");

            var serviceFrame = message.Pop ();                  // [DATA]
            var serviceName = serviceFrame.ConvertToString ();
            var service = ServiceRequired (serviceName);

            Wrap (sender, message);                             // [CLIENT ADR][e][DATA]

            if (serviceFrame.BufferSize >= 4)
            {
                var returnCode = "501";

                if (serviceName == "mmi.service")
                {
                    var name = message.Last.ConvertToString ();

                    if (m_services.Exists (s => s.Name == name))
                    {
                        var svc = m_services.Find (s => s.Name == name);

                        returnCode = svc.DoWorkersExist () ? "200" : "400";
                    }
                }
                // set the return code to be the last frame in the message
                var ret = new NetMQFrame (returnCode);
                message.RemoveFrame (message.Last);             // [CLIENT ADR][e]
                message.Append (ret);                           // [CLIENT ADR][e][return code]
                // remove client return envelope and insert protocol header and service name, then rewrap envelope
                var client = UnWrap (message);              // [return code]
                message.Push (MDPClientHeader);             // [protocol header][return code]
                Wrap (client, message);                     // [CLIENT ADR][e][protocol header][return code]
                // send to back to CLIENT(!)
                Socket.SendMessage (message);
            }
            else
                // send to a worker offering the requested service
                ServiceDispatch (service, message);         // [CLIENT ADR][e][DATA]
        }

        /// <summary>
        ///     This method deletes any idle workers that haven't pinged us in a
        ///     while. We hold workers from oldest to most recent so we can stop
        ///     scanning whenever we find a live worker. This means we'll mainly stop
        ///     at the first worker, which is essential when we have large numbers of
        ///     workers (we call this method in our critical path)
        /// </summary>
        public void Purge ()
        {
            foreach (var service in m_services)
                foreach (var worker in service.WaitingWorkers)
                {
                    if (DateTime.UtcNow < worker.Expiry)
                        // we found the first woker not expired in that service
                        // any following worker will be younger -> we're done for the service
                        break;

                    OnLogInfoReady (new LogInfoEventArgs { LogInfo = string.Format ("[BROKER] Deleting expired worker {0}", worker.Id) });

                    RemoveWorker (worker);
                }
        }

        /// <summary>
        ///     send a heartbeat to all known active worker
        /// </summary>
        public void SendHeartbeat ()
        {
            Purge ();

            foreach (var worker in m_knownWorkers)
                WorkerSend (worker, MDPCommand.Heartbeat, null);
        }

        /// <summary>
        ///     adds the worker to the service and the known worker list
        ///     if not already known
        /// </summary>
        private void AddWorker (Worker worker, Service service)
        {
            worker.Expiry = DateTime.UtcNow + m_heartbeatExpiry;

            if (!m_knownWorkers.Contains (worker))
                m_knownWorkers.Add (worker);

            service.AddWaitingWorker (worker);
            // get pending messages out
            ServiceDispatch (service, null);
        }

        /// <summary>
        ///     removes the worker from the known worker list and
        ///     from the service it referes to if it exists and
        ///     potentially from the waiting list therein
        /// </summary>
        /// <param name="worker"></param>
        private void RemoveWorker (Worker worker)
        {
            if (m_services.Contains (worker.Service))
            {
                var service = m_services.Find (s => s == worker.Service);
                service.DeleteWorker (worker);
            }

            m_knownWorkers.Remove (worker);
        }

        /// <summary>
        ///     sends a message to a specific worker with a specific command 
        ///     and add an option option
        /// </summary>
        private void WorkerSend (Worker worker, MDPCommand command, NetMQMessage message, string option = null)
        {
            var msg = ReferenceEquals (message, null) ? new NetMQMessage () : message;

            // stack protocol envelope to start of message
            if (!ReferenceEquals (option, null))
                msg.Push (option);

            msg.Push (new[] { (byte) command });
            msg.Push (MDPWorkerHeader);
            // stack routing envelope
            Wrap (worker.Identity, message);

            OnLogInfoReady (new LogInfoEventArgs { LogInfo = string.Format ("[BROKE] Sending {0}", msg) });
            // send to worker
            Socket.SendMessage (msg);
        }

        /// <summary>
        ///     locates service by name or creates new service if there 
        ///     is no service available with that name
        /// </summary>
        /// <param name="serviceName">the service requested</param>
        /// <returns>the requested service object</returns>
        private Service ServiceRequired (string serviceName)
        {
            return m_services.Exists (s => s.Name == serviceName)
                       ? m_services.Find (s => s.Name == serviceName)
                       : new Service (serviceName);
        }

        /// <summary>
        ///     sends requests to waiting workers of the requested service
        ///     if no worker is available this request is added to the pending requests
        ///     for this service
        ///     it also removes all expired workers of the service
        /// </summary>
        private void ServiceDispatch (Service service, NetMQMessage message)
        {
            if (!ReferenceEquals (message, null))
                service.AddRequest (message);

            // remove all expired workers!
            Purge ();

            Worker worker;
            while ((worker = service.GetNextWorker ()) != null)
            {
                if (service.PendingRequests.Count > 0)
                {
                    var request = service.GetNextRequest ();

                    WorkerSend (worker, MDPCommand.Request, request);
                }
                else
                    // no more requests -> we're done
                    break;
            }
        }

        /// <summary>
        ///     if a logging information shall be broadcasted
        ///     raise the event if someone is listening
        /// </summary>
        /// <param name="e">the wrapped logging information</param>
        protected virtual void OnLogInfoReady (LogInfoEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }

        /// <summary>
        ///     returns the first frame and deletes the next frame if it is empty
        /// </summary>
        private NetMQFrame UnWrap (NetMQMessage message)
        {
            var frame = message.Pop ();

            if (message.First == NetMQFrame.Empty)
                message.Pop ();

            return frame;
        }

        /// <summary>
        ///     adds an empty frame and a specified frame to a message
        /// </summary>
        private void Wrap (NetMQFrame frame, NetMQMessage message)
        {
            if (frame.BufferSize > 0)
            {
                message.Push (NetMQFrame.Empty);
                message.Push (frame);
            }
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
                m_ctx.Dispose ();
            }
            // get rid of unmanaged resources
        }

        #endregion IDisposable
    }
}
