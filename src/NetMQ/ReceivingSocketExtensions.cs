using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public static class ReceivingSocketExtensions
    {
        public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            return socket.Receive(options.HasFlag(SendReceiveOptions.DontWait), out hasMore);
        }

        public static byte[] Receive(this IReceivingSocket socket, out bool hasMore)
        {
            return socket.Receive(false, out hasMore);
        }

        public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.Receive(options, out hasMore);
        }

        public static byte[] Receive(this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.Receive(false, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {
            byte[] data = socket.Receive(dontWait, out hasMore);
            return Encoding.ASCII.GetString(data);
        }

        public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            return socket.ReceiveString(options.HasFlag(SendReceiveOptions.DontWait), out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.ReceiveString(options, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, out bool hasMore)
        {
            return socket.ReceiveString(false, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.ReceiveString(false, out hasMore);
        }

        /// <summary>
        /// Receive from the given IReceivingSocket, blocking and waiting until data is available,
        /// and return the data as a NetMQMessage.
        /// This is the same as calling ReceiveMessage with a dontWait argument value of false.
        /// </summary>
        /// <param name="socket">the IReceivingSocket to receive bytes from</param>
        /// <returns>a new NetMQMessage that contains what was received from the socket</returns>
        public static NetMQMessage ReceiveMessage(this IReceivingSocket socket)
        {
            NetMQMessage message = new NetMQMessage();
            socket.ReceiveMessage(message, false);
            return message;
        }

        /// <summary>
        /// Receive from the given IReceivingSocket and return the result as a NetMQMessage.
        /// </summary>
        /// <param name="socket">the IReceivingSocket to receive bytes from</param>
        /// <param name="dontWait">if true, socket.Receive is called without blocking - it returns immediately</param>
        /// <returns>a new NetMQMessage that contains what was received from the socket</returns>
        public static NetMQMessage ReceiveMessage(this IReceivingSocket socket, bool dontWait)
        {
            NetMQMessage message = new NetMQMessage();
            socket.ReceiveMessage(message, dontWait);
            return message;
        }

        public static NetMQMessage ReceiveMessage(this NetMQSocket socket, TimeSpan timeout)
        {
            var item = new PollItem(socket.SocketHandle, PollEvents.PollIn);
            var items = new[] { item };
            ZMQ.Poll(items, (int)timeout.TotalMilliseconds);

            if (item.ResultEvent.HasFlag(PollEvents.PollError) && !socket.IgnoreErrors)
                throw new ErrorPollingException("Error while polling", socket);

            if (!item.ResultEvent.HasFlag(PollEvents.PollIn))
                return null;

            var msg = socket.ReceiveMessage();
            return msg;
        }

        /// <summary>
        /// Receive from the given socket into a NetMQMessage, waiting for a message if there is none that has arrived yet.
        /// This is the same as calling ReceiveMessage within a dontWait argument value of false.
        /// </summary>
        /// <param name="socket">the IReceivingSocket to receive bytes from</param>
        /// <param name="message">the NetMQMessage to receive the bytes into</param>
        public static void ReceiveMessage(this IReceivingSocket socket, NetMQMessage message)
        {
            ReceiveMessage(socket, message, false);
        }

        /// <summary>
        /// Receive from the given socket into a NetMQMessage.
        /// </summary>
        /// <param name="socket">the IReceivingSocket to receive bytes from</param>
        /// <param name="message">the NetMQMessage to receive the bytes into</param>
        /// <param name="dontWait">if true, socket.Receive is called without blocking - it returns immediately</param>
        public static void ReceiveMessage(this IReceivingSocket socket, NetMQMessage message, bool dontWait)
        {
            message.Clear();

            bool more = true;

            while (more)
            {
                byte[] buffer = socket.Receive(dontWait, out more);
                message.Append(buffer);
            }
        }

        public static IEnumerable<byte[]> ReceiveMessages(this IReceivingSocket socket)
        {
            bool hasMore = true;

            while (hasMore)
                yield return socket.Receive(false, out hasMore);
        }

        public static IEnumerable<string> ReceiveStringMessages(this IReceivingSocket socket)
        {
            bool hasMore = true;

            while (hasMore)
                yield return socket.ReceiveString(SendReceiveOptions.None, out hasMore);
        }

        [Obsolete("Use ReceiveMessages extension method instead")]
        public static IList<byte[]> ReceiveAll(this IReceivingSocket socket)
        {
            return socket.ReceiveMessages().ToList();
        }

        [Obsolete("Use ReceiveStringMessages extension method instead")]
        public static IList<string> ReceiveAllString(this IReceivingSocket socket)
        {
            return socket.ReceiveStringMessages().ToList();
        }
    }
}
