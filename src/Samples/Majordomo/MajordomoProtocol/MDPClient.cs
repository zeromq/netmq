using System;
using System.Text;

using NetMQ;

using MDPCommons;
using NetMQ.Sockets;

namespace MajordomoProtocol
{
    /// <summary>
    ///     implements a client skeleton for Majordomo Protocol V0.1
    /// </summary>
    public class MDPClient : IMDPClient
    {
        private readonly string m_mdpClient = MDPBroker.MDPClientHeader;

        private NetMQSocket m_client;           // the socket to communicate with the broker

        private readonly string m_brokerAddress;
        private readonly byte[] m_identity;
        private bool m_connected;               // used as flag true if a connection has been made
        private string m_serviceName;           // need that as storage for the event handler
        private NetMQMessage m_reply;           // container for the received reply from broker

        /// <summary>
        ///     sets or gets the timeout period for waiting for messages
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        ///     sets or gets the number of tries before the communication
        ///     is deemed to be lost
        /// </summary>
        public int Retries { get; set; }

        /// <summary>
        ///     returns the address of the broker the client is connected to
        /// </summary>
        public string Address { get { return m_brokerAddress; } }

        /// <summary>
        ///     returns the name of the client
        /// </summary>
        public byte[] Identity { get { return m_identity; } }

        /// <summary>
        ///     if client has a log message available if fires this event
        /// </summary>
        public event EventHandler<MDPLogEventArgs> LogInfoReady;

        /// <summary>
        ///     setup the client with standard values
        ///     verbose == false
        ///     timeout == 2500
        ///     reties  == 3
        /// </summary>
        private MDPClient ()
        {
            m_client = null;
            Timeout = TimeSpan.FromMilliseconds (2500);
            Retries = 3;
            m_connected = false;
        }

        /// <summary>
        ///     setup the client, use standard values and parameters
        /// </summary>
        /// <param name="brokerAddress">address the broker can be connected to</param>
        /// <param name="identity">if present will become the name for the client socket, encoded in UTF8</param>
        public MDPClient (string brokerAddress, byte[] identity = null)
            : this ()
        {
            if (string.IsNullOrWhiteSpace (brokerAddress))
                throw new ArgumentNullException ("brokerAddress", "The broker address must not be null, empty or whitespace!");

            m_identity = identity;
            m_brokerAddress = brokerAddress;
        }

        /// <summary>
        ///     setup the client, use standard values and parameters
        /// </summary>
        /// <param name="brokerAddress">address the broker can be connected to</param>
        /// <param name="identity">sets the name of the client (must be UTF8), if empty or white space it is ignored</param>
        public MDPClient (string brokerAddress, string identity)
        {
            if (string.IsNullOrWhiteSpace (brokerAddress))
                throw new ArgumentNullException ("brokerAddress", "The broker address must not be null, empty or whitespace!");

            if (!string.IsNullOrWhiteSpace (identity))
                m_identity = Encoding.UTF8.GetBytes (identity);

            m_brokerAddress = brokerAddress;
        }

        /// <summary>
        ///     send a request to a broker for a specific service and receive the reply
        ///
        ///     if the reply is not available within a specified time
        ///     the client deems the connection as lost and reconnects
        ///     for a specified number of times. if no reply is available
        ///     throughout this procedure the client abandons
        ///     the reply is checked for validity and returns the reply
        ///     according to the verbose flag it reports about its activities
        /// </summary>
        /// <param name="serviceName">the name of the service requested</param>
        /// <param name="request">the request message to process by service</param>
        /// <returns>the reply from service</returns>
        /// <exception cref="ApplicationException">malformed message received</exception>
        /// <exception cref="ApplicationException">malformed header received</exception>
        /// <exception cref="ApplicationException">reply received from wrong service</exception>
        public NetMQMessage Send (string serviceName, NetMQMessage request)
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ApplicationException ("serviceName must not be empty or null.");

            if (ReferenceEquals (request, null))
                throw new ApplicationException ("the request must not be null");
            // memorize it for the event handler
            m_serviceName = serviceName;

            // if for any reason the socket is NOT connected -> connect it!
            if (!m_connected)
                Connect ();

            var message = new NetMQMessage (request);

            // prefix the request according to MDP specs
            // Frame 1: "MDPCxy" (six bytes MDP/Client x.y)
            // Frame 2: service name as printable string
            // Frame 3: request
            message.Push (serviceName);
            message.Push (m_mdpClient);

            Log (string.Format ("[CLIENT INFO] sending {0} to service {1}", message, serviceName));

            var retiesLeft = Retries;

            while (retiesLeft > 0)
            {
                // beware of an exception if broker has not picked up the message at all
                // because one can not send multiple times! it is strict REQ -> REP -> REQ ...
                m_client.SendMultipartMessage (message);

                // Poll -> see ReceiveReady for event handling
                if (m_client.Poll (Timeout))
                {
                    // set by event handler
                    return m_reply;
                }
                // if it failed assume communication dead and re-connect
                if (--retiesLeft > 0)
                {
                    Log ("[CLIENT WARNING] no reply, reconnecting ...");

                    Connect ();
                }
            }

            Log ("[CLIENT ERROR] permanent error, abandoning!");

            m_client.Dispose ();

            return null;
        }

        /// <summary>
        ///     connects to the broker, if a socket already exists it will be disposed and
        ///     a new socket created and connected
        ///     MDP requires a REQUEST socket for a client
        /// </summary>
        /// <exception cref="ApplicationException">NetMQContext must not be <c>null</c></exception>
        /// <exception cref="ApplicationException">if broker address is empty or <c>null</c></exception>
        private void Connect ()
        {
            if (!ReferenceEquals (m_client, null))
                m_client.Dispose ();

            m_client = new RequestSocket ();

            if (m_identity != null)
                m_client.Options.Identity = m_identity;

            // attach the event handler for incoming messages
            m_client.ReceiveReady += ProcessReceiveReady;

            m_client.Connect (m_brokerAddress);

            m_connected = true;

            Log (string.Format ("[CLIENT] connecting to broker at {0}", m_brokerAddress));
        }

        /// <summary>
        ///     handle the incoming messages
        /// </summary>
        /// <remarks>
        ///     socket strips [client adr][e] from message
        ///     message -> [protocol header][service name][reply]
        ///                [protocol header][service name][result code of service lookup]
        /// </remarks>
        private void ProcessReceiveReady (object sender, NetMQSocketEventArgs e)
        {
            // a message is available within the timeout period
            var reply = m_client.ReceiveMultipartMessage ();

            Log (string.Format ("\n[CLIENT INFO] received the reply {0}\n", reply));

            // in production code malformed messages should be handled smarter
            if (reply.FrameCount < 2)
                throw new ApplicationException ("[CLIENT ERROR] received a malformed reply");

            var header = reply.Pop (); // [MDPHeader] <- [service name][reply] OR ['mmi.service'][return code]

            if (header.ConvertToString () != m_mdpClient)
                throw new ApplicationException (string.Format ("[CLIENT INFO] MDP Version mismatch: {0}", header));

            var service = reply.Pop (); // [service name or 'mmi.service'] <- [reply] OR [return code]

            if (service.ConvertToString () != m_serviceName)
                throw new ApplicationException (string.Format ("[CLIENT INFO] answered by wrong service: {0}",
                                                               service.ConvertToString ()));
            // now set the value for the reply of the send method!
            m_reply = reply;        // [reply] OR [return code]
        }

        private void Log (string info)
        {
            if (!string.IsNullOrWhiteSpace (info))
                OnLogInfoReady (new MDPLogEventArgs { Info = info });
        }

        /// <summary>
        ///     broadcast the logging information if someone is listening
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnLogInfoReady (MDPLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
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

            // m_client might not have been created yet!
            if (!ReferenceEquals (m_client, null))
                m_client.Dispose ();
        }
    }
}
