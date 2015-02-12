using System;
using System.Net;
using NetMQ;

using MajordomoProtocol.Contracts;

namespace MajordomoProtocol
{
    /// <summary>
    ///     implements a client skeleton for Majordomo Protocol V0.1
    /// </summary>
    public class MDPClient : IMajordomoClient, IDisposable
    {
        // according to MDP the header of a request must have the first frame with
        // a string stating:
        //      "MDP" for the protocol
        //      "C" for Client
        //      "01" for the version of the client V0.1
        private const string _MDP_CLIENT = "MDPC01";

        private readonly NetMQContext m_ctx;
        private NetMQSocket m_client;           // the socket to communicate with the broker

        private readonly string m_brokerAddress;
        private bool m_connected;               // used as flag true if a connection has been made
        private string m_serviceName;           // need that as storage for the event handler
        private NetMQMessage m_reply;           // container for the received reply from broker
        // set in the event handler(!)

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
        ///     if client has a log message available if fires this event
        /// </summary>
        public event EventHandler<LogInfoEventArgs> LogInfoReady;

        /// <summary>
        ///     setup the client with standard values
        ///     verbose == false
        ///     timeout == 2500
        ///     reties  == 3
        /// </summary>
        private MDPClient ()
        {
            m_ctx = NetMQContext.Create ();
            m_client = null;
            Timeout = TimeSpan.FromMilliseconds (2500);
            Retries = 3;
            m_connected = false;
        }

        /// <summary>
        ///     setup the client, use standard values and parameters
        /// </summary>
        /// <param name="brokerAddress">address the broker can be connected to</param>
        public MDPClient (string brokerAddress)
            : this ()
        {
            if (string.IsNullOrWhiteSpace (brokerAddress))
                throw new ArgumentNullException ("brokerAddress", "The broker address must not be null, empty or whitespace!");

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

            var msg = new NetMQMessage (request);

            // prefix the request according to MDP specs
            // Frame 1: "MDPCxy" (six bytes MDP/Client x.y)
            // Frame 2: service name as printable string
            // Frame 3: request
            msg.Push (serviceName);
            msg.Push (_MDP_CLIENT);

            OnLogInfoReady (new LogInfoEventArgs
                            {
                                LogInfo = string.Format ("[CLIENT INFO] sending {0} to service {1}", msg, serviceName)
                            });

            var retiesLeft = Retries;

            while (retiesLeft > 0)
            {
                // beaware of an exception if broker has not picked up the message at all
                // because one can not send multiple times! it is strict REQ -> REP -> REQ ...
                m_client.SendMessage (msg);

                // Poll -> must have ReceiveReady!!!!!!

                if (m_client.Poll (Timeout))
                {
                    // set by event handler
                    return m_reply;
                }
                // if it failed assume communication dead and re-connect
                if (--retiesLeft > 0)
                {
                    OnLogInfoReady (new LogInfoEventArgs { LogInfo = "[CLIENT WARNING] no reply, reconnecting ..." });

                    Connect ();
                }
            }

            OnLogInfoReady (new LogInfoEventArgs { LogInfo = "[CLIENT ERROR] permanent error, abandoning!" });

            m_client.Dispose ();
            m_ctx.Dispose ();

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

            if (ReferenceEquals (m_ctx, null))
                throw new ApplicationException ("NetMQContext does not exist!");

            m_client = m_ctx.CreateRequestSocket ();
            // attach the event handler for incoming messages
            m_client.ReceiveReady += ProcessReceiveReady;

            m_client.Connect (m_brokerAddress);

            m_connected = true;

            OnLogInfoReady (new LogInfoEventArgs
                            {
                                LogInfo = string.Format ("[CLIENT] connecting to broker at {0}", m_brokerAddress)
                            });
        }

        /// <summary>
        ///     handle the incoming messages
        /// </summary>
        private void ProcessReceiveReady (object sender, NetMQSocketEventArgs e)
        {
            // a message is available within the timeout period
            var reply = m_client.ReceiveMessage ();

            OnLogInfoReady (new LogInfoEventArgs
            {
                LogInfo = string.Format ("[CLIENT INFO] received the reply {0}", reply)
            });

            // in production code malformed messages should be handled smarter
            if (reply.FrameCount < 3)
                throw new ApplicationException ("[CLIENT ERROR] received a malformed reply");

            var header = reply.Pop ();

            if (header.ConvertToString () != _MDP_CLIENT)
                throw new ApplicationException (string.Format ("[CLIENT INFO] MDP Version mismatch: {0}", header));

            var service = reply.Pop ();

            if (service.ConvertToString () != m_serviceName)
                throw new ApplicationException (string.Format ("[CLIENT INFO] answered by wrong service: {0}",
                                                               service.ConvertToString ()));
            // now set the value for the reply of the send method!
            m_reply = reply;
        }

        /// <summary>
        ///     broadcast the logging information if someone is listening
        /// </summary>
        /// <param name="e"></param>
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
                // m_client might not have been created yet!
                if (!ReferenceEquals (m_client, null))
                    m_client.Dispose ();
                m_ctx.Dispose ();
            }
            // get rid of unmanaged resources
        }

        #endregion IDisposable
    }
}
