using System;
using JetBrains.Annotations;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A RequestSocket is a NetMQSocket intended to be used as the Request part of the Request-Response pattern.
    /// This is generally paired with a ResponseSocket.
    /// </summary>
    public class RequestSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new RequestSocket and attach socket to zero or more endpoints.
        /// </summary>
        /// <param name="connectionString">List of NetMQ endpoints, separated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is connect (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new RequestSocket(">tcp://127.0.0.1:5555,@127.0.0.1:55556");</code></example>
        public RequestSocket(string connectionString = null) : base(ZmqSocketType.Req, connectionString, DefaultAction.Connect)
        {
        }

        /// <summary>
        /// Create a new RequestSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal RequestSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        public enum ProgressTopic
        {
            Send,
            Retry,
            Failure,
            Success
        }

        /// <summary>
        /// Try to send request message and return the response as a message, or return null if not successful
        /// </summary>
        /// <param name="address">a string denoting the address to connect to</param>
        /// <param name="requestMessage">The request message</param>
        /// <param name="numTries">The number of times to try</param>
        /// <param name="requestTimeout">The timeout for each request</param>
        /// <param name="progressPublisher">Report topics: Failure, Retry, Send, Success</param>
        /// <returns>the response message, or null if not successful</returns>
        public static NetMQMessage RequestResponseMultipartMessageWithRetry([NotNull] string address, [NotNull] NetMQMessage requestMessage,
            int numTries, TimeSpan requestTimeout, PublisherSocket progressPublisher = null)
        {
            var responseMessage = new NetMQMessage();

            while (numTries-- > 0)
            {
                using (var requestSocket = new RequestSocket(address))
                {
                    progressPublisher?.SendFrame(ProgressTopic.Send.ToString());

                    requestSocket.SendMultipartMessage(requestMessage);

                    if (requestSocket.TryReceiveMultipartMessage(requestTimeout, ref responseMessage))
                    {
                        progressPublisher?.SendFrame(ProgressTopic.Success.ToString());

                        return responseMessage;
                    }

                    progressPublisher?.SendFrame(ProgressTopic.Retry.ToString());
                }
            }

            progressPublisher?.SendFrame(ProgressTopic.Failure.ToString());

            return null;
        }

        /// <summary>
        /// Try to send request string and return the response string, or return null if not successful
        /// </summary>
        /// <param name="address">a string denoting the address to connect to</param>
        /// <param name="requestString">The request string</param>
        /// <param name="numTries">The number of times to try</param>
        /// <param name="requestTimeout">The timeout for each request</param>
        /// <param name="progressPublisher">Report topics: Failure, Retry, Send, Success</param>
        /// <returns>the response message, or null if not successful</returns>
        public static string RequestResponseStringWithRetry([NotNull] string address, [NotNull] string requestString,
            int numTries, TimeSpan requestTimeout, PublisherSocket progressPublisher = null)
        {
            while (numTries-- > 0)
            {
                using (var requestSocket = new RequestSocket(address))
                {
                    progressPublisher?.SendFrame(ProgressTopic.Send.ToString());

                    requestSocket.SendFrame(requestString);

                    if (requestSocket.TryReceiveFrameString(requestTimeout, out string frameString))
                    {
                        progressPublisher?.SendFrame(ProgressTopic.Success.ToString());

                        return frameString;
                    }

                    progressPublisher?.SendFrame(ProgressTopic.Retry.ToString());
                }
            }

            progressPublisher?.SendFrame(ProgressTopic.Failure.ToString());

            return null;
        }
    }
}
