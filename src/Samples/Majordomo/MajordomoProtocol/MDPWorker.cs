using System;
using System.Threading;

using NetMQ;

using MDPCommons;
using NetMQ.Sockets;


namespace MajordomoProtocol
{
    public class MDPWorker : IMDPWorker
    {
        // Majordomo protocol header
        private readonly string m_mdpWorker = MDPBroker.MDPWorkerHeader;

        private const int _heartbeat_liveliness = 3;// indicates the remaining "live" for the worker

        private readonly string m_brokerAddress;    // the broker address to connect to
        private readonly string m_serviceName;      // the name of the service the worker offers
        private NetMQSocket m_worker;               // the worker socket itself -MDP requires to use DEALER
        private DateTime m_heartbeatAt;             // when to send HEARTBEAT
        private int m_liveliness;                   // how many attempts are left
        private int m_expectReply;                  // will be 0 at start
        private NetMQFrame m_returnIdentity;        // the return identity if any
        private bool m_exit;                        // a flag for exiting the worker
        private bool m_connected;                   // a flag to signal whether worker is connected to broker or not
        private NetMQMessage m_request;             // used to collect the request received from the ReceiveReady event handler
        private readonly int m_connectionRetries;   // the number of times the worker tries to connect to the broker before it abandons
        private int m_retriesLeft;                  // indicates the number of connection retries that are left
        private readonly byte[] m_identity;         // if not null the identity of the worker socket

        /// <summary>
        ///     send a heartbeat every specified milliseconds
        /// </summary>
        public TimeSpan HeartbeatDelay { get; set; }

        /// <summary>
        ///     delay in milliseconds between reconnects
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; }

        /// <summary>
        ///     broadcast logging information via this event
        /// </summary>
        public event EventHandler<MDPLogEventArgs> LogInfoReady;

        /// <summary>
        ///     create worker with standard parameter
        ///     HeartbeatDelay == 2500 milliseconds
        ///     ReconnectDelay == 2500 milliseconds
        ///     ConnectionRetries == 3
        ///     Verbose == false
        /// </summary>
        private MDPWorker ()
        {
            HeartbeatDelay = TimeSpan.FromMilliseconds (2500);
            ReconnectDelay = TimeSpan.FromMilliseconds (2500);
            m_exit = false;
            m_connected = false;
        }

        /// <summary>
        ///     creates worker with standard parameter and
        ///     set the broker's address and the service name for the worker
        /// </summary>
        /// <param name="brokerAddress">the address the worker can connect to the broker at</param>
        /// <param name="serviceName">the service the worker offers</param>
        /// <param name="identity">the identity of the worker, if present - set automatic otherwise</param>
        /// <param name="connectionRetries">the number of times the worker tries to connect to the broker - default 3</param>
        /// <exception cref="ArgumentNullException">The address of the broker must not be null, empty or whitespace!</exception>
        /// <exception cref="ArgumentNullException">The name of the service must not be null, empty or whitespace!</exception>
        /// <remarks>
        ///     create worker with standard parameter
        ///     HeartbeatDelay == 2500 milliseconds
        ///     ReconnectDelay == 2500 milliseconds
        ///     Verbose == false
        ///
        ///     the worker will try to connect <c>connectionRetries</c> cycles to establish a
        ///     connection to the broker, within each cycle he tries it 3 times with <c>ReconnectDelay</c>
        ///     delay between each cycle
        /// </remarks>
        public MDPWorker (string brokerAddress, string serviceName, byte[] identity = null, int connectionRetries = 3)
            : this ()
        {
            if (string.IsNullOrWhiteSpace (brokerAddress))
                throw new ArgumentNullException ("brokerAddress",
                                                 "The address of the broker must not be null, empty or whitespace!");

            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName",
                                                 "The name of the service must not be null, empty or whitespace!");
            m_identity = identity;
            m_brokerAddress = brokerAddress;
            m_serviceName = serviceName;
            m_connectionRetries = connectionRetries;
        }

        /// <summary>
        ///     initially sends a READY message to the broker upon connection
        ///     and waits for a request to come
        /// </summary>
        /// <param name="reply">the reply to send</param>
        /// <returns>the request to process</returns>
        /// <remarks>
        ///     if it is the initial call to Receive - reply must be <c>null</c> in order to
        ///     send READY and subsequently wait for a request.
        ///     reply == <c>null</c> will bypass the sending of a message!
        /// </remarks>
        public NetMQMessage Receive (NetMQMessage reply)
        {
            // set the number of left retries to connect
            m_retriesLeft = m_connectionRetries;

            if (!m_connected)
                Connect ();

            // since Connect send the READY we are waiting after a Connect for a
            // REQ and must skip the REPLY step
            // if however the Connect has not been called than we have received
            // and processed a REQ and must send a REP and at one must be pending
            if (!ReferenceEquals (reply, null) && m_expectReply != 0)
            {
                if (ReferenceEquals (m_returnIdentity, null) || m_returnIdentity.BufferSize == 0)
                    throw new ApplicationException ("A malformed reply has been provided");

                var message = Wrap (reply, m_returnIdentity);       // [client id][e][reply]

                Send (MDPCommand.Reply, null, message);
            }

            m_expectReply = 1;
            // now wait for the next request
            while (!m_exit)
            {
                // see ReceiveReady event handler -> ProcessReceiveReady
                if (m_worker.Poll (HeartbeatDelay))
                {
                    // a request has been received, so connection is established - reset the connection retries
                    m_retriesLeft = m_connectionRetries;
                    // ProcessReceiveReady will set m_request only if a request arrived
                    if (!ReferenceEquals (m_request, null))
                        return m_request;
                }
                else
                {
                    // if m_liveliness times no message has been received -> try to reconnect
                    if (--m_liveliness == 0)
                    {
                        // if we tried it _HEARTBEAT_LIVELINESS * m_connectionRetries times without
                        // success therefor we deem the broker dead or the communication broken
                        // and abandon the worker
                        if (--m_retriesLeft < 0)
                        {
                            Log ("[WORKER] abandoning worker due to errors!");
                            break;
                        }

                        Log ("[WORKER INFO] disconnected from broker - retrying ...");

                        // wait before reconnecting
                        Thread.Sleep (HeartbeatDelay);
                        // reconnect
                        Connect ();
                    }

                    // if it is time to send a heartbeat to broker
                    if (DateTime.UtcNow >= m_heartbeatAt)
                    {
                        Send (MDPCommand.Heartbeat, null, null);
                        // set new point in time for sending the next heartbeat
                        m_heartbeatAt = DateTime.UtcNow + HeartbeatDelay;
                    }
                }
            }

            if (m_exit)
                Log ("[WORKER] abandoning worker due to request!");

            m_worker.Dispose ();

            return null;
        }

        /// <summary>
        ///     if a logging information shall be broadcasted
        ///     raise the event if someone is listening
        /// </summary>
        /// <param name="e">the wrapped logging information</param>
        protected virtual void OnLogInfoReady (MDPLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }

        /// <summary>
        ///     upon arrival of a message process it
        ///     and set the request variable accordingly
        /// </summary>
        /// <remarks>
        ///     worker expects to receive either of the following
        ///     REQUEST     -> [e][header][command][client adr][e][request]
        ///     HEARTBEAT   -> [e][header][command]
        ///     DISCONNECT  -> [e][header][command]
        ///     KILL        -> [e][header][command]
        /// </remarks>
        protected virtual void ProcessReceiveReady (object sender, NetMQSocketEventArgs e)
        {
            // a request has arrived process it
            var request = m_worker.ReceiveMultipartMessage ();

            Log (string.Format ("[WORKER] received {0}", request));

            // make sure that we have no valid request yet!
            // if something goes wrong we'll return 'null'
            m_request = null;
            // any message from broker is treated as heartbeat(!) since communication exists
            m_liveliness = _heartbeat_liveliness;
            // check the expected message envelope and get the embedded MPD command
            var command = GetMDPCommand (request);
            // MDP command is one byte!
            switch (command)
            {
                case MDPCommand.Request:
                    // the message is [client adr][e][request]
                    // save as many addresses as there are until we hit an empty frame
                    // - for simplicity assume it is just one
                    m_returnIdentity = Unwrap (request);
                    // set the class variable in order to return the request to caller
                    m_request = request;
                    break;
                case MDPCommand.Heartbeat:
                    // reset the liveliness of the broker
                    m_liveliness = _heartbeat_liveliness;
                    break;
                case MDPCommand.Disconnect:
                    // reconnect the worker
                    Connect ();
                    break;
                case MDPCommand.Kill:
                    // stop working you worker you
                    m_exit = true;
                    break;
                default:
                    Log ("[WORKER ERROR] invalid command received!");
                    break;
            }
        }

        /// <summary>
        /// Check the message envelope for errors
        /// and get the MDP Command embedded.
        /// The message will be altered!
        /// </summary>
        /// <param name="request">NetMQMessage received</param>
        /// <returns>the received MDP Command</returns>
        private MDPCommand GetMDPCommand (NetMQMessage request)
        {
            // don't try to handle errors
            if (request.FrameCount < 3)
                throw new ApplicationException ("Malformed request received!");

            var empty = request.Pop ();

            if (!empty.IsEmpty)
                throw new ApplicationException ("First frame must be an empty frame!");

            var header = request.Pop ();

            if (header.ConvertToString () != m_mdpWorker)
                throw new ApplicationException ("Invalid protocol header received!");

            var cmd = request.Pop ();

            if (cmd.BufferSize > 1)
                throw new ApplicationException ("MDP Command must be one byte not multiple!");

            var command = (MDPCommand) cmd.Buffer[0];

            Log (string.Format ("[WORKER] received {0}/{1}", request, command));

            return command;
        }

        /// <summary>
        /// Connect or re-connect to the broker.
        /// </summary>
        private void Connect ()
        {
            // if the socket exists dispose it and re-create one
            if (!ReferenceEquals (m_worker, null))
            {
                m_worker.Unbind (m_brokerAddress);
                m_worker.Dispose ();
            }

            m_worker = new DealerSocket ();
            // set identity if provided
            if (m_identity != null && m_identity.Length > 0)
                m_worker.Options.Identity = m_identity;

            // hook up the received message processing method before the socket is connected
            m_worker.ReceiveReady += ProcessReceiveReady;

            m_worker.Connect (m_brokerAddress);

            Log (string.Format ("[WORKER] connected to broker at {0}", m_brokerAddress));

            // signal that worker is connected
            m_connected = true;
            // send READY to broker since worker is connected
            Send (MDPCommand.Ready, m_serviceName, null);
            // reset liveliness to active broker
            m_liveliness = _heartbeat_liveliness;
            // set point in time for next heatbeat
            m_heartbeatAt = DateTime.UtcNow + HeartbeatDelay;
        }

        /// <summary>
        /// Send a message to broker
        /// if no message provided create a new empty one
        /// prepend the message with the MDP prologue
        /// </summary>
        /// <param name="mdpCommand">MDP command</param>
        /// <param name="data">data to be sent</param>
        /// <param name="message">the message to send</param>
        private void Send (MDPCommand mdpCommand, string data, NetMQMessage message)
        {
            // cmd, null, message      -> [REPLY],<null>,[client adr][e][reply]
            // cmd, string, null       -> [READY],[service name]
            // cmd, null, null         -> [HEARTBEAT]
            var msg = ReferenceEquals (message, null) ? new NetMQMessage () : message;
            // protocol envelope according to MDP
            // last frame is the data if available
            if (!ReferenceEquals (data, null))
            {
                // data could be multiple whitespaces or even empty(!)
                msg.Push (data);
            }
            // set MDP command                          ([client adr][e][reply] OR [service]) => [data]
            msg.Push (new[] { (byte) mdpCommand });     // [command][header][data]
            // set MDP Header
            msg.Push (m_mdpWorker);                     // [header][data]
            // set MDP empty frame as separator
            msg.Push (NetMQFrame.Empty);                // [e][command][header][data]

            Log (string.Format ("[WORKER] sending {0} to broker / Command {1}", msg, mdpCommand));

            m_worker.SendMultipartMessage (msg);
        }

        /// <summary>
        /// Prepend the message with an empty frame as separator and a frame
        /// </summary>
        /// <returns>new message with wrapped content</returns>
        private static NetMQMessage Wrap (NetMQMessage msg, NetMQFrame frame)
        {
            var result = new NetMQMessage (msg);

            result.Push (NetMQFrame.Empty);            // according to MDP an empty frame is the separator
            result.Push (frame);                       // the return address

            return result;
        }

        /// <summary>
        /// Strip the message from the first frame and if empty the following frame as well
        /// </summary>
        /// <returns>the first frame of the message</returns>
        private static NetMQFrame Unwrap (NetMQMessage msg)
        {
            var result = msg.Pop ();

            if (msg.First.IsEmpty)
                msg.Pop ();

            return result;
        }

        /// <summary>
        /// Log the info via registered event handler.
        /// </summary>
        private void Log (string info)
        {
            if (!string.IsNullOrWhiteSpace (info))
                OnLogInfoReady (new MDPLogEventArgs { Info = info });
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        protected virtual void Dispose (bool disposing)
        {
            if (!disposing)
                return;

            if (!ReferenceEquals (m_worker, null))
                m_worker.Dispose ();
        }
    }
}
