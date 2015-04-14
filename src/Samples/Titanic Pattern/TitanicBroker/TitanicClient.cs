using System;
using System.Text;
using System.Threading;

using JetBrains.Annotations;

using NetMQ;

using MajordomoProtocol;
using MDPCommons;
using TitanicCommons;

namespace TitanicProtocol
{
    /// <summary>
    ///     provides TitanicClient API
    /// </summary>
    public class TitanicClient : ITitanicClient
    {
        private IMDPClient m_client = null;

        /// <summary>
        ///     the hook to attach observers for logging information
        /// </summary>
        public event EventHandler<TitanicLogEventArgs> LogInfoReady;

        /// <summary>
        ///     ctor - creates a titanic client API object
        ///     for easy access to titanic services
        /// </summary>
        /// <param name="address">a valid NetMQ address string</param>
        /// <remarks>
        ///     valid NetMQ address string are i.e.
        /// 
        ///         tcp:\\localhost:5555
        ///         tcp:\\172.12.2.34
        ///     
        ///     or alike for more information 
        ///     <see cref="http://netmq.readthedocs.org/en/latest/transports/"/>
        /// </remarks>
        public TitanicClient (string address)
        {
            m_client = new MDPClient (address);
        }

        /// <summary>
        ///     ctor - creates a titanic client API object
        ///     for easy access to titanic services
        /// </summary>
        /// <param name="client">a valid Major Domo Protocol Client</param>
        public TitanicClient (IMDPClient client)
        {
            m_client = client;
        }

        /// <summary>
        ///     send a request and get the reply
        /// </summary>
        /// <param name="serviceName">the name of the service requested</param>
        /// <param name="request">the data forming the request</param>
        /// <param name="retries">is the max. reties until aborted, default == 3</param>
        /// <param name="waitInBetween">time in milliseconds to wait inbetween retries, default == 2500</param>
        /// <returns>the reply of the service</returns>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Tuple<byte[], TitanicReturnCode> GetResult (string serviceName,
                                                           byte[] request,
                                                           int retries = 3,
                                                           int waitInBetween = 2500)
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            Log (string.Format ("requesting service {0} to process {1}", serviceName, request));

            var count = 0;
            var requestId = Guid.Empty;
            while (count < retries)
            {
                // 1. get the request id for the request made
                requestId = Request (serviceName, request);

                count++;
                // has anything gone wrong?
                if (requestId == Guid.Empty)
                {
                    // now the client is not usable anymore -> needs to be recreated
                    RecreateClient ();
                    // if we have reached max retries, return the failure else retry
                    if (count == retries)
                        return new Tuple<byte[], TitanicReturnCode> (null, TitanicReturnCode.Failure);
                }

            }
            Log (string.Format ("RequestId = {0}", requestId));

            // 2. wait for reply
            var reply = Reply (requestId, retries, TimeSpan.FromMilliseconds (waitInBetween));

            // mark as closed
            CloseRequest (requestId);

            if (reply == null)
            {
                Log ("ERROR: CORRUPTED REPLY BY TITANIC - PANIC!");

                return null;
            }

            return reply;
        }

        /// <summary>
        ///     send a request and get the reply
        /// </summary>
        /// <param name="serviceName">the name of the service requested</param>
        /// <param name="request">the data forming the request</param>
        /// <param name="retries">is the max. reties until aborted, default == 3</param>
        /// <param name="waitInBetween">time in milliseconds to wait inbetween retries, default == 2500</param>
        /// <returns>the reply of the service</returns>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Tuple<byte[], TitanicReturnCode> GetResult (string serviceName,
                                                           string request,
                                                           int retries = 3,
                                                           int waitInBetween = 2500)
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            return GetResult (serviceName, Encoding.UTF8.GetBytes (request), retries, waitInBetween);
        }

        /// <summary>
        ///     places a request with a specified service and retrieves the reply in one step
        /// </summary>
        /// <param name="serviceName">the name of the service requested</param>
        /// <param name="request">the data forming the request</param>
        /// <param name="enc">the encoding request is encoded with</param>
        /// <param name="retries">is the max. reties until aborted, default == 3</param>
        /// <param name="waitInBetween">time in milliseconds to wait inbetween retries, default == 2500</param>
        /// <returns>the reply of the service</returns>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Tuple<string, TitanicReturnCode> GetResult (string serviceName,
                                                           string request,
                                                           Encoding enc,
                                                           int retries = 3,
                                                           int waitInBetween = 2500)
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            var reply = GetResult (serviceName, enc.GetBytes (request), retries, waitInBetween);

            return new Tuple<string, TitanicReturnCode> (enc.GetString (reply.Item1), reply.Item2);
        }

        /// <summary>
        ///     places a request with a specified service and retrieves the reply in one step
        /// </summary>
        /// <typeparam name="TIn">the type handed in</typeparam>
        /// <typeparam name="TOut">the type returned</typeparam>
        /// <param name="serviceName">name of service requested</param>
        /// <param name="request">the request's data</param>
        /// <param name="retries">number of retries for collecting the reply, default == 3</param>
        /// <param name="waitInBetween">milliseconds between retires, default == 2500</param>
        /// <returns>a tuple containing the return type and a status for interpreting the type's value</returns>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Tuple<TOut, TitanicReturnCode> GetResult<TIn, TOut> (string serviceName,
                                                                    TIn request,
                                                                    int retries = 3,
                                                                    int waitInBetween = 2500)
            where TIn : ITitanicConvert<TIn>
            where TOut : ITitanicConvert<TOut>, new ()
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            var reply = GetResult (serviceName, request.ConvertToBytes (), retries, waitInBetween);
            var result = new TOut ();

            result.GenerateFrom (reply.Item1);

            return new Tuple<TOut, TitanicReturnCode> (result, reply.Item2);
        }

        /// <summary>
        ///     requests an GUID from titanic for a request of a service
        /// </summary>
        /// <param name="serviceName">service requested</param>
        /// <param name="request">data to process by that service</param>
        /// <returns>GUID or 'null' if it fails</returns>
        /// <remarks>
        ///     byte[] are considered to be encoded UTF-8
        /// </remarks>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Guid Request (string serviceName, byte[] request)
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            var message = new NetMQMessage ();
            // set request data
            message.Push (request);                   // [data]
            // set requested service
            message.Push (serviceName);              // [service name][data]

            Log (string.Format ("requesting: {0}", message));

            var reply = ServiceCall (m_client, TitanicOperation.Request, message);

            if (reply == null)
                return Guid.Empty;

            if (reply.Item2 == TitanicReturnCode.Failure)
                return Guid.Empty;

            var id = reply.Item1.Pop ().ConvertToString ();

            return new Guid (id);
        }

        /// <summary>
        ///     requests an GUID from titanic for a request of a service
        /// </summary>
        /// <param name="serviceName">service requested</param>
        /// <param name="request">data to process by that service, will be converted to byte[] with UTF-8</param>
        /// <returns>GUID or 'null' if it fails</returns>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Guid Request (string serviceName, string request)
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            return Request (serviceName, Encoding.UTF8.GetBytes (request));
        }

        /// <summary>
        ///     requests an GUID from titanic for a request of a service
        /// </summary>
        /// <param name="serviceName">service requested</param>
        /// <param name="request">data to process by that service, will be converted to byte[] with UTF-8</param>
        /// <returns>GUID or 'null' if it fails</returns>
        /// <remarks>
        ///     'T' must implement 'ITitanicConvert' in order to be processed
        /// </remarks>
        /// <exception cref="ArgumentNullException">The name of the service requested must not be empty or 'null'!</exception>
        public Guid Request<T> (string serviceName, T request) where T : ITitanicConvert<T>
        {
            if (string.IsNullOrWhiteSpace (serviceName))
                throw new ArgumentNullException ("serviceName", "The name of the service requested must not be empty or 'null'!");

            return Request (serviceName, request.ConvertToBytes ());
        }

        /// <summary>
        ///     gets a result for a request, if one is available
        /// </summary>
        /// <param name="requestId">GUID of the request who's reply is sought</param>
        /// <param name="retries">max. number of tries the reply is asked for, default == 3</param>
        /// <param name="waitBetweenRetries">milliseconds to wait in between tries</param>
        /// <returns>
        ///     a tuple containing the reply and the status for interpreting the value of the reply
        ///     <list type="bullet">
        ///         <listheader>
        ///             <term>Combination</term>
        ///             <desciption>Combinations of ReturnCode and interpretation of data</desciption>
        ///         </listheader>
        ///         <item>
        ///             <term>OK        -> valid reply</term>
        ///         </item>
        ///         <item>
        ///             <term>Pending   -> no meaning reply</term>
        ///         </item>
        ///         <item>
        ///             <term>Unknown   -> service unknown - no meaning reply</term>
        ///         </item>
        ///         <item>
        ///             <term>Failure   -> invalid reply</term>
        ///         </item>
        ///     </list>
        ///             
        /// </returns>
        /// <exception cref="ArgumentNullException">The id of the request must not be empty or 'null'!</exception>
        public Tuple<byte[], TitanicReturnCode> Reply (Guid requestId, int retries, TimeSpan waitBetweenRetries)
        {
            if (requestId == Guid.Empty)
                throw new ArgumentNullException ("requestId", "The id of the request must not be empty!");

            var message = new NetMQMessage ();
            var rc = TitanicReturnCode.Failure;

            retries = retries <= 0 ? 3 : retries;

            for (var i = 0; i < retries; i++)
            {
                message.Push (requestId.ToString ());

                Log (string.Format ("requesting reply for: {0}", message));

                var reply = ServiceCall (m_client, TitanicOperation.Reply, message);

                if (reply.Item2 == TitanicReturnCode.Ok)
                    return new Tuple<byte[], TitanicReturnCode> (reply.Item1.First.Buffer, reply.Item2);

                Thread.Sleep (waitBetweenRetries);

                rc = reply.Item2;
            }

            return new Tuple<byte[], TitanicReturnCode> (null, rc);
        }

        /// <summary>
        ///     retrieves the reply which may either be ready or or pending
        /// </summary>
        /// <param name="requestId">id of the request</param>
        /// <param name="waitFor">milliseconds to wait for a reply</param>
        /// <returns>
        ///     a tuple with the reply as byte array and a state for 
        ///     interpreting the reply's content
        /// </returns>
        /// <exception cref="ArgumentNullException">The id of the request must not be empty or 'null'!</exception>
        public Tuple<byte[], TitanicReturnCode> Reply (Guid requestId, TimeSpan waitFor)
        {
            if (requestId == Guid.Empty)
                throw new ArgumentNullException ("requestId", "The id of the request must not be empty!");

            var retries = waitFor.Milliseconds > 5000 ? 8 : 4;
            var waitBetween = waitFor.Milliseconds / retries;

            return Reply (requestId, retries, TimeSpan.FromMilliseconds (waitBetween));
        }

        /// <summary>
        ///     retrieves the reply which may either be ready or or pending
        /// </summary>
        /// <typeparam name="T">the return type for this request</typeparam>
        /// <param name="requestId">id of the request</param>
        /// <param name="waitFor">milliseconds to wait for a reply</param>
        /// <returns>a tuple with the type and a state for interpreting the types content</returns>
        /// <exception cref="ArgumentNullException">The id of the request must not be empty or 'null'!</exception>
        public Tuple<T, TitanicReturnCode> Reply<T> (Guid requestId, TimeSpan waitFor) where T : ITitanicConvert<T>, new ()
        {
            if (requestId == Guid.Empty)
                throw new ArgumentNullException ("requestId", "The id of the request must not be empty!");

            var reply = Reply (requestId, waitFor);

            var result = new T ();

            return new Tuple<T, TitanicReturnCode> (result.GenerateFrom (reply.Item1), reply.Item2);
        }

        /// <summary>
        ///     retrieves the reply which may either be ready or or pending
        /// </summary>
        /// <typeparam name="T">the return type for this request</typeparam>
        /// <param name="requestId">id of the request</param>
        /// <param name="retries">max. number of tries the reply is asked for, default == 3</param>
        /// <param name="waitBetweenRetries">milliseconds to wait in between tries, default == 2500</param>
        /// <returns>a tuple with the type and a state for interpreting the types content</returns>
        /// <exception cref="ArgumentNullException">The id of the request must not be empty or 'null'!</exception>
        public Tuple<T, TitanicReturnCode> Reply<T> (Guid requestId, int retries, TimeSpan waitBetweenRetries)
            where T : ITitanicConvert<T>, new ()
        {
            if (requestId == Guid.Empty)
                throw new ArgumentNullException ("requestId", "The id of the request must not be empty!");

            var reply = Reply (requestId, retries, waitBetweenRetries);

            var result = new T ();

            return new Tuple<T, TitanicReturnCode> (result.GenerateFrom (reply.Item1), reply.Item2);
        }

        /// <summary>
        ///     marks a request as closed - no further processing and will be deleted any time in the future
        ///     the request may be in any state
        /// </summary>
        /// <param name="requestId">id of the request to close</param>
        public void CloseRequest (Guid requestId)
        {
            if (requestId == Guid.Empty)
                return;

            var message = new NetMQMessage ();
            message.Push (requestId.ToString ());

            Log (string.Format ("request close: {0}", message));

            ServiceCall (m_client, TitanicOperation.Close, message);
        }

        private Tuple<NetMQMessage, TitanicReturnCode> ServiceCall (IMDPClient session, TitanicOperation op, NetMQMessage message)
        {
            // message can be:
            //  REQUEST:
            //      in goes -> [titanic operation][service requested][request]
            //      returns -> [return code][Guid]
            //  REPLY
            //      in goes -> [titanic operation][request id]
            //      returns -> [return code][reply]
            //  CLOSE
            //      in goes -> [titanic operation][request id]
            var reply = session.Send (op.ToString (), message);

            Log (string.Format ("received message: {0}", reply));

            if (ReferenceEquals (reply, null) || reply.IsEmpty)
                return null; // went wrong - why? I don't care!

            // we got a reply -> [return code][data]
            var rc = reply.Pop ().ConvertToString ();       // [return code] <- [data] or [service name] if 'Unknown'
            var status = (TitanicReturnCode) Enum.Parse (typeof (TitanicReturnCode), rc);

            switch (status)
            {
                case TitanicReturnCode.Ok:
                    return new Tuple<NetMQMessage, TitanicReturnCode> (reply, TitanicReturnCode.Ok);
                case TitanicReturnCode.Pending:
                    return new Tuple<NetMQMessage, TitanicReturnCode> (reply, TitanicReturnCode.Pending);
                case TitanicReturnCode.Unknown:
                    Log ("ERROR: Service unknown!");
                    return new Tuple<NetMQMessage, TitanicReturnCode> (reply, TitanicReturnCode.Unknown);
                default:
                    Log ("ERROR: FATAL ERROR ABANDONING!");
                    return new Tuple<NetMQMessage, TitanicReturnCode> (null, TitanicReturnCode.Failure);
            }
        }

        private void Log ([NotNull] string info)
        {
            OnLogInfoReady (new TitanicLogEventArgs { Info = "[TITANIC CLIENT] " + info });
        }

        protected virtual void OnLogInfoReady (TitanicLogEventArgs e)
        {
            var handler = LogInfoReady;

            if (handler != null)
                handler (this, e);
        }

        private void RecreateClient ()
        {
            var adr = m_client.Address;
            var id = m_client.Identity;

            m_client.Dispose ();

            // recreate the client with the old config values
            m_client = new MDPClient (adr, id);
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
                m_client.Dispose ();
            }
            // get rid of unmanaged resources
        }

        #endregion IDisposable
    }
}
