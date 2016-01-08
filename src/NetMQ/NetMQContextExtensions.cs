using System;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ
{
    /// <summary>
    /// This static class serves to provide extension methods for NetMQContext.
    /// </summary>
    public static class NetMQContextExtensions
    {
        /// <summary>
        /// Try to send request message and return the response as a message, or return null if not successful
        /// </summary>
        /// <param name="context">The <paramref cref="NetMQContext"/></param>
        /// <param name="address">a string denoting the address to connect to</param>
        /// <param name="requestMessage">The request message</param>
        /// <param name="numTries">The number of times to try</param>
        /// <param name="requestTimeout">The timeout for each request</param>
        /// <param name="progressReporter">Set to true to report progress to Console</param>
        /// <returns>the response message, or null if not successful</returns>
        public static NetMQMessage RequestResponseMultipartMessageWithRetry([NotNull] this NetMQContext context, [NotNull] string address,
            [NotNull] NetMQMessage requestMessage, int numTries, TimeSpan requestTimeout, IProgress<string> progressReporter = null)
        {
            var responseMessage = new NetMQMessage();
            var requestSocket = GetNewRequestSocket(context, address);
            try
            {
                while (numTries-- > 0)
                {
                    if (progressReporter != null)
                    {
                        progressReporter.Report("Sending message");
                    }
                    requestSocket.SendMultipartMessage(requestMessage);
                    if (requestSocket.TryReceiveMultipartMessage(requestTimeout, ref responseMessage))
                    {
                        if (progressReporter != null)
                        {
                            progressReporter.Report("Server replied OK");
                        }
                        return responseMessage;
                    }
                    if (progressReporter != null)
                    {
                        progressReporter.Report("No response from server, retrying...");
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
            if (progressReporter != null)
            {
                progressReporter.Report("Server seems to be offline, abandoning");
            }
            return null;
        }

        private static RequestSocket GetNewRequestSocket(NetMQContext netMQContext, string address)
        {
            var requestSocket = netMQContext.CreateRequestSocket();
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
