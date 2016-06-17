using System;
using System.Text;

using NetMQ;

using MDPCommons;
using NetMQ.Sockets;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace MajordomoProtocol
{
    /// <summary>
    ///     implements a client skeleton for Majordomo Protocol V0.1
    /// </summary>
    public class MDPClientAsync : IMDPClientAsync
    {
        private readonly TimeSpan m_defaultTimeOut = TimeSpan.FromMilliseconds(10000); // default value to be used to know if broker is not responding
        private readonly TimeSpan m_lingerTime = TimeSpan.FromMilliseconds(1);
        private readonly string m_mdpClient = MDPConstants.MDP_CLIENT_HEADER;

        private NetMQSocket m_client;           // the socket to communicate with the broker
        private NetMQPoller m_poller;           // used to poll asynchronously the replies and possible timeouts

        private readonly string m_brokerAddress;
        private readonly byte[] m_identity;
        private bool m_connected;               // used as flag true if a connection has been made
        private string m_serviceName;           // need that as storage for the event handler

        private NetMQTimer m_timer;             // used to know if broker is not responding

        /// <summary>
        ///     returns the address of the broker the client is connected to
        /// </summary>
        public string Address => m_brokerAddress;

        /// <summary>
        ///     returns the name of the client
        /// </summary>
        public byte[] Identity => m_identity;

        /// <summary>
        ///     if client has a log message available it fires this event
        /// </summary>
        public event EventHandler<MDPLogEventArgs> LogInfoReady;

        /// <summary>
        ///     if client has a reply message available it fires this event
        /// </summary>
        public event EventHandler<MDPReplyEventArgs> ReplyReady;

        /// <summary>
        ///     sets or gets the timeout period that a client can stay connected 
        ///     without receiving any messages from broker
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        ///     setup the client with standard values
        ///     verbose == false
        ///     Connect the client to broker
        /// </summary>
        private MDPClientAsync()
        {
            m_client = null;
            m_connected = false;

            m_poller = new NetMQPoller();
            Timeout = m_defaultTimeOut;

            m_timer = new NetMQTimer(Timeout);
            m_timer.Enable = false;
            m_poller.Add(m_timer);
            m_timer.Elapsed += (s, e) => OnProcessTimeOut();
            m_poller.RunAsync();
        }

        /// <summary>
        ///     setup the client, use standard values and parameters and connects client to broker
        /// </summary>
        /// <param name="brokerAddress">address the broker can be connected to</param>
        /// <param name="identity">if present will become the name for the client socket, encoded in UTF8</param>
        public MDPClientAsync(string brokerAddress, byte[] identity = null)
            : this()
        {
            if (string.IsNullOrWhiteSpace(brokerAddress))
                throw new ArgumentNullException(nameof(brokerAddress), "The broker address must not be null, empty or whitespace!");

            m_identity = identity;
            m_brokerAddress = brokerAddress;
        }

        /// <summary>
        ///     setup the client, use standard values and parameters and connects client to broker
        /// </summary>
        /// <param name="brokerAddress">address the broker can be connected to</param>
        /// <param name="identity">sets the name of the client (must be UTF8), if empty or white space it is ignored</param>
        public MDPClientAsync([NotNull] string brokerAddress, string identity)
        {
            if (string.IsNullOrWhiteSpace(brokerAddress))
                throw new ArgumentNullException(nameof(brokerAddress), "The broker address must not be null, empty or whitespace!");

            if (!string.IsNullOrWhiteSpace(identity))
                m_identity = Encoding.UTF8.GetBytes(identity);

            m_brokerAddress = brokerAddress;
        }

        /// <summary>
        ///     send a asyncronous request to a broker for a specific service without receiving a reply
        ///     
        ///     there is no retry logic
        ///     according to the verbose flag it reports about its activities
        /// </summary>
        /// <param name="serviceName">the name of the service requested</param>
        /// <param name="request">the request message to process by service</param>
        /// <exception cref="ApplicationException">serviceName must not be empty or null.</exception>
        /// <exception cref="ApplicationException">the request must not be null</exception>
        public void Send([NotNull] string serviceName, NetMQMessage request)
        {
            // TODO criar tabela de pedidos ongoing

            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ApplicationException("serviceName must not be empty or null.");

            if (ReferenceEquals(request, null))
                throw new ApplicationException("the request must not be null");

            // memorize it for the event handler
            m_serviceName = serviceName;

            // if for any reason the socket is NOT connected -> connect it!
            if (!m_connected)
                Connect();

            var message = new NetMQMessage(request);
            // prefix the request according to MDP specs
            // Frame 1: Empty Frame, because DEALER socket needs to emulate a REQUEST socket to comunicate with ROUTER socket
            // Frame 2: "MDPCxy" (six bytes MDP/Client x.y)
            // Frame 3: service name as printable string
            // Frame 4: request
            message.Push(serviceName);
            message.Push(m_mdpClient);
            message.PushEmptyFrame();

            Log($"[CLIENT INFO] sending {message} to service {serviceName}");

            m_client.SendMultipartMessage(message); // Or TrySend!? ErrorHandling!?
        }

        /// <summary>
        ///     broadcast the logging information if someone is listening
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnLogInfoReady(MDPLogEventArgs e)
        {
            LogInfoReady?.Invoke(this, e);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            // m_poller might not have been created yet!
            if (!ReferenceEquals(m_poller, null))
                m_poller.Dispose();

            // m_client might not have been created yet!
            if (!ReferenceEquals(m_client, null))
                m_client.Dispose();
        }

        /// <summary>
        ///     connects to the broker, if a socket already exists it will be disposed and
        ///     a new socket created and connected (Linger is set to 1)
        ///     The Client connects to broker using a DEALER socket
        /// </summary>
        private void Connect()
        {
            if (!ReferenceEquals(m_client, null))
            {
                m_poller.Remove(m_client);
                m_client.Dispose();
            }
               
            m_client = new DealerSocket();

            // sets a low linger to avoid port exhaustion, messages can be lost since it'll be sent again
            m_client.Options.Linger = m_lingerTime;

            if (m_identity != null)
                m_client.Options.Identity = m_identity;

            // set timeout timer to reconnect if no message is received during timeout
            m_timer.EnableAndReset(); 

            // attach the event handler for incoming messages
            m_client.ReceiveReady += OnProcessReceiveReady;
            m_poller.Add(m_client);

            // TODO Define HighWaterMark!?
            m_client.Connect(m_brokerAddress);

            m_connected = true;

            Log($"[CLIENT] connecting to broker at {m_brokerAddress}");
        }

        /// <summary>
        ///     handle the incoming messages
        ///     if a message is received timeout timer is reseted
        /// </summary>
        /// <remarks>
        ///     socket strips [client adr][e] from message // TODO remove this?!
        ///     message -> 
        ///                [empty frame][protocol header][service name][reply]
        ///                [empty frame][protocol header][service name][result code of service lookup]
        /// </remarks>
        private void OnProcessReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            Exception exception = null;
            NetMQMessage reply = null;
            try
            {
                reply = m_client.ReceiveMultipartMessage();
                if (ReferenceEquals(reply, null))
                    throw new ApplicationException("Unexpected behavior");

                m_timer.EnableAndReset(); // reset timeout timer because a message was received

                Log($"[CLIENT INFO] received the reply {reply}");

                ExtractFrames(reply);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ReturnReply(reply, exception);
            }
        }

        /// <summary>
        ///   verifies if the message replied obeys the MDP 0.2 protocol
        /// </summary>
        /// <remarks>
        ///     socket strips [client adr][e] from message
        ///     message -> 
        ///                [empty frame][protocol header][service name][reply]
        ///                [empty frame][protocol header][service name][result code of service lookup]
        /// </remarks>
        private void ExtractFrames(NetMQMessage reply)
        {
            if (reply.FrameCount < 4)
                throw new ApplicationException("[CLIENT ERROR] received a malformed reply");

            var emptyFrame = reply.Pop();
            if (emptyFrame != NetMQFrame.Empty)
            {
                throw new ApplicationException($"[CLIENT ERROR] received a malformed reply expected empty frame instead of: { emptyFrame } ");
            }
            var header = reply.Pop(); // [MDPHeader] <- [service name][reply] OR ['mmi.service'][return code]

            if (header.ConvertToString() != m_mdpClient)
                throw new ApplicationException($"[CLIENT INFO] MDP Version mismatch: {header}");

            var service = reply.Pop(); // [service name or 'mmi.service'] <- [reply] OR [return code]

            if (service.ConvertToString() != m_serviceName)
                throw new ApplicationException($"[CLIENT INFO] answered by wrong service: {service.ConvertToString()}");
        }

        private void OnProcessTimeOut()
        {
            Log($"[CLIENT INFO] No message received from broker during {Timeout} time");

            // TODO only reconnect if there is an outgoing request!?
            Connect();
            // TODO track outgoing requests and make a retry mechanism
        }

        /// <summary>
        ///     broadcast the logging information if someone is listening
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnReplyReady(MDPReplyEventArgs e)
        {
            ReplyReady?.Invoke(this, e);
        }

        private void ReturnReply(NetMQMessage reply, Exception exception = null)
        {
            OnReplyReady(new MDPReplyEventArgs(reply, exception));
        }

        private void Log(string info)
        {
            if (!string.IsNullOrWhiteSpace(info))
                OnLogInfoReady(new MDPLogEventArgs { Info = info });
        }
    }
}
