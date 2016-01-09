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
        /// <param name="context">The <paramref cref="NetMQContext"/></param>
        /// <param name="address">a string denoting the address to connect to</param>
        /// <param name="requestMessage">The request message</param>
        /// <param name="numTries">The number of times to try</param>
        /// <param name="requestTimeout">The timeout for each request</param>
        /// <param name="progressPublisher">Report topics: Failure, Retry, Send, Success</param>
        /// <returns>the response message, or null if not successful</returns>
        public static NetMQMessage RequestResponseMultipartMessageWithRetry([NotNull] NetMQContext context, [NotNull] string address,
            [NotNull] NetMQMessage requestMessage, int numTries, TimeSpan requestTimeout, PublisherSocket progressPublisher = null)
        {
            var responseMessage = new NetMQMessage();
            var requestSocket = GetNewRequestSocket(context, address);
            try
            {
                while (numTries-- > 0)
                {
                    if (progressPublisher != null)
                    {
                        progressPublisher.SendFrame(ProgressTopic.Send.ToString());
                    }
                    requestSocket.SendMultipartMessage(requestMessage);
                    if (requestSocket.TryReceiveMultipartMessage(requestTimeout, ref responseMessage))
                    {
                        if (progressPublisher != null)
                        {
                            progressPublisher.SendFrame(ProgressTopic.Success.ToString());
                        }
                        return responseMessage;
                    }
                    if (progressPublisher != null)
                    {
                        progressPublisher.SendFrame(ProgressTopic.Retry.ToString());
                    }

                    // Try again. The Lazy Pirate pattern is to destroy the socket and create a new one.
                    TerminateSocket(requestSocket, address);
                    requestSocket = GetNewRequestSocket(context, address);
                }
            }
            finally
            {
                TerminateSocket(requestSocket, address);
            }
            if (progressPublisher != null)
            {
                progressPublisher.SendFrame(ProgressTopic.Failure.ToString());
            }
            return null;
        }

        /// <summary>
        /// Try to send request string and return the response string, or return null if not successful
        /// </summary>
        /// <param name="context">The <paramref cref="NetMQContext"/></param>
        /// <param name="address">a string denoting the address to connect to</param>
        /// <param name="requestString">The request string</param>
        /// <param name="numTries">The number of times to try</param>
        /// <param name="requestTimeout">The timeout for each request</param>
        /// <param name="progressPublisher">Report topics: Failure, Retry, Send, Success</param>
        /// <returns>the response message, or null if not successful</returns>
        public static string RequestResponseStringWithRetry([NotNull] NetMQContext context, [NotNull] string address,
            [NotNull] string requestString, int numTries, TimeSpan requestTimeout, PublisherSocket progressPublisher = null)
        {
            var requestSocket = GetNewRequestSocket(context, address);
            try
            {
                while (numTries-- > 0)
                {
                    if (progressPublisher != null)
                    {
                        progressPublisher.SendFrame(ProgressTopic.Send.ToString());
                    }
                    requestSocket.SendFrame(requestString);
                    string frameString;
                    if (requestSocket.TryReceiveFrameString(requestTimeout, out frameString))
                    {
                        if (progressPublisher != null)
                        {
                            progressPublisher.SendFrame(ProgressTopic.Success.ToString());
                        }
                        return frameString;
                    }
                    if (progressPublisher != null)
                    {
                        progressPublisher.SendFrame(ProgressTopic.Retry.ToString());
                    }

                    // Try again. The Lazy Pirate pattern is to destroy the socket and create a new one.
                    TerminateSocket(requestSocket, address);
                    requestSocket = GetNewRequestSocket(context, address);
                }
            }
            finally
            {
                TerminateSocket(requestSocket, address);
            }
            if (progressPublisher != null)
            {
                progressPublisher.SendFrame(ProgressTopic.Failure.ToString());
            }
            return null;
        }

        private static RequestSocket GetNewRequestSocket(NetMQContext context, string address)
        {
            var requestSocket = context.CreateRequestSocket();
            requestSocket.Connect(address);
            requestSocket.Options.Linger = TimeSpan.Zero;
            return requestSocket;
        }

        private static void TerminateSocket(NetMQSocket requestSocket, string address)
        {
            requestSocket.Disconnect(address);
            requestSocket.Close();
        }
    }
}
