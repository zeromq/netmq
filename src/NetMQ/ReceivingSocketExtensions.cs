using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NetMQ.Core;


namespace NetMQ
{
    public static class ReceivingSocketExtensions
    {
        #region Byte Array

        public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg, options);

            byte[] data = new byte[msg.Size];

            if (msg.Size > 0)
            {
                Buffer.BlockCopy(msg.Data, 0, data, 0, msg.Size);    
            }
            
            hasMore = msg.HasMore;

            msg.Close();

            return data;            
        }

        public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.Receive(options, out hasMore);
        }

        public static byte[] Receive(this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {           
            SendReceiveOptions options =SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            return socket.Receive(options, out hasMore);  
        }
      
        public static byte[] Receive(this IReceivingSocket socket, out bool hasMore)
        {
            return socket.Receive(false, out hasMore);
        }
       
        public static byte[] Receive(this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.Receive(false, out hasMore);
        }

        public static IEnumerable<byte[]> ReceiveMessages(this IReceivingSocket socket)
        {
            bool hasMore = true;

            while (hasMore)
                yield return socket.Receive(false, out hasMore);
        }

        #endregion

        #region Strings

        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, SendReceiveOptions options, out bool hasMore)
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg, options);

            hasMore = msg.HasMore;

            string data = string.Empty;

            if (msg.Size > 0)
            {
                data = encoding.GetString(msg.Data, 0, msg.Size);
            }

            msg.Close();

            return data;
        }

        public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            return socket.ReceiveString(Encoding.ASCII, options, out hasMore);
        }        

        public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.ReceiveString(options, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.ReceiveString(encoding, options, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {
            return ReceiveString(socket, Encoding.ASCII, dontWait, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, bool dontWait, out bool hasMore)
        {
            SendReceiveOptions options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            return socket.ReceiveString(encoding, options, out hasMore);
        }
        
        public static string ReceiveString(this IReceivingSocket socket, out bool hasMore)
        {
            return socket.ReceiveString(false, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, out bool hasMore)
        {
            return socket.ReceiveString(encoding, false, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding)
        {
            bool hasMore;
            return socket.ReceiveString(encoding, false, out hasMore);
        }

        public static string ReceiveString(this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.ReceiveString(false, out hasMore);
        }

        public static string ReceiveString(this NetMQSocket socket, TimeSpan timeout)
        {
            return ReceiveString(socket, Encoding.ASCII, timeout);
        }

        public static string ReceiveString(this NetMQSocket socket, Encoding encoding, TimeSpan timeout)
        {                        
            var result = socket.SocketHandle.Poll(PollEvents.PollIn, (int) timeout.TotalMilliseconds);           

            if (!result.HasFlag(PollEvents.PollIn))
                return null;

            var msg = socket.ReceiveString(encoding);
            return msg;
        }

        public static IEnumerable<string> ReceiveStringMessages(this IReceivingSocket socket)
        {
            return ReceiveStringMessages(socket, Encoding.ASCII);
        }

        public static IEnumerable<string> ReceiveStringMessages(this IReceivingSocket socket, Encoding encoding)
        {
            bool hasMore = true;

            while (hasMore)
                yield return socket.ReceiveString(encoding, SendReceiveOptions.None, out hasMore);
        }

        #endregion

        #region NetMQMessge

        public static void ReceiveMessage(this IReceivingSocket socket, NetMQMessage message, bool dontWait = false)
        {
            message.Clear();

            bool more = true;

            while (more)
            {
                byte[] buffer = socket.Receive(dontWait, out more);
                message.Append(buffer);
            }
        }

        public static NetMQMessage ReceiveMessage(this IReceivingSocket socket, bool dontWait = false)
        {
            NetMQMessage message = new NetMQMessage();
            socket.ReceiveMessage(message, dontWait);
            return message;
        }

        public static NetMQMessage ReceiveMessage(this NetMQSocket socket, TimeSpan timeout)
        {
            var result = socket.SocketHandle.Poll(PollEvents.PollIn, (int)timeout.TotalMilliseconds);

            if (!result.HasFlag(PollEvents.PollIn))
                return null;

            var msg = socket.ReceiveMessage();
            return msg;
        }
        
        #endregion

        #region Signals

        public static bool WaitForSignal(this IReceivingSocket socket)
        {
            while (true)
            {
                var message = socket.ReceiveMessage();

                if (message.FrameCount == 1 && message.First.MessageSize == 8)
                {
                    long signalValue = message.First.ConvertToInt64();

                    if ((signalValue & 0x7FFFFFFFFFFFFF00L) == 0x7766554433221100L)
                    {
                        return (signalValue & 255) == 0;
                    }
                }
            }
        }

        #endregion

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
