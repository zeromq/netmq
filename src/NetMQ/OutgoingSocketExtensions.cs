using System;
using System.Text;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    public static class OutgoingSocketExtensions
    {
        #region Byte Array

        public static void Send(this IOutgoingSocket socket, [NotNull] byte[] data, int length, SendReceiveOptions options)
        {
            Msg msg = new Msg();
            msg.InitPool(length);

            Buffer.BlockCopy(data, 0, msg.Data, 0, length);

            socket.Send(ref msg, options);

            msg.Close();
        }        

        public static void Send(this IOutgoingSocket socket, [NotNull] byte[] data, int length, bool dontWait = false, bool sendMore = false)
        {            
            SendReceiveOptions options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            if (sendMore)
            {
                options|= SendReceiveOptions.SendMore;
            }

            socket.Send(data, length, options);         
        }        

        public static void Send(this IOutgoingSocket socket, [NotNull] byte[] data)
        {
            socket.Send(data, data.Length);
        }

        [NotNull]
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, [NotNull] byte[] data, bool dontWait = false)
        {
            socket.Send(data, data.Length, dontWait, true);
            return socket;
        }

        [NotNull]
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, [NotNull] byte[] data, int length, bool dontWait = false)
        {
            socket.Send(data, length, dontWait, true);
            return socket;
        }

        #endregion

        #region Strings

        public static void Send(this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, SendReceiveOptions options)
        {
            Msg msg = new Msg();
            msg.InitPool(encoding.GetByteCount(message));

            encoding.GetBytes(message, 0, message.Length, msg.Data, 0);

            socket.Send(ref msg, options);

            msg.Close();
        }

        public static void Send(this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, bool dontWait = false, bool sendMore = false)
        {            
            SendReceiveOptions options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            if (sendMore)
            {
                options |= SendReceiveOptions.SendMore;
            }

            socket.Send(message, encoding, options);   
        }

        public static void Send(this IOutgoingSocket socket, [NotNull] string message, bool dontWait = false, bool sendMore = false)
        {
            Send(socket, message, Encoding.ASCII, dontWait, sendMore);
        }
        
        [NotNull]
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, [NotNull] string message, bool dontWait = false)
        {
            socket.Send(message, false, true);
            return socket;
        }

        [NotNull]
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, bool dontWait = false)
        {
            socket.Send(message,encoding, false, true);
            return socket;
        }

        #endregion

        #region NetMQMessage

        public static void SendMessage(this IOutgoingSocket socket, [NotNull] NetMQMessage message, bool dontWait = false)
        {
            for (int i = 0; i < message.FrameCount - 1; i++)
            {
                socket.Send(message[i].Buffer, message[i].MessageSize, dontWait, true);
            }

            socket.Send(message.Last.Buffer, message.Last.MessageSize, dontWait);
        }       

        #endregion

        #region Signals

        private static void Signal(this IOutgoingSocket socket, byte status)
        {
            long signalValue = 0x7766554433221100L + status;
            NetMQMessage message = new NetMQMessage();
            message.Append(signalValue);

            socket.SendMessage(message);
        }

        public static void SignalOK(this IOutgoingSocket socket)
        {
            Signal(socket, 0);
        }

        public static void SignalError(this IOutgoingSocket socket)
        {
            Signal(socket, 1);
        }


        #endregion       
    }
}
