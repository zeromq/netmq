Receiving / Sending
=====

If you have read the [Introduction] (https://github.com/zeromq/netmq/blob/master/docs/Introdcution.md) page you would have
already seen an example of <code>ReceiveString()</code> and <code>SendString()</code>, but NetMQ allows us to send more than just strings.

In fact there are quite a few options available to you. Lets go through some of these options shall we



Receiving
=====

A <code>NetMQSocket</code> (which all the socket types inherit from) has a single <code>public virtual void Receive(ref Msg msg, SendReceiveOptions options)</code> method. But you will likely not actually use this
method. What you will end up using is one the extra extension methods that is available for IOutgoingSocket. These extension methods are shown below, one of these should give you what you want, but if it doesn't
you simply need to write an extra extension method to suit your needs

    public static class ReceivingSocketExtensions
    {
        public static byte[] Receive(this IReceivingSocket socket);
        public static byte[] Receive(this IReceivingSocket socket, out bool hasMore);
        public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options);
        public static byte[] Receive(this IReceivingSocket socket, bool dontWait, out bool hasMore);
        public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore);
        public static NetMQMessage ReceiveMessage(this IReceivingSocket socket, bool dontWait = false);
        public static NetMQMessage ReceiveMessage(this NetMQSocket socket, TimeSpan timeout);
        public static void ReceiveMessage(this IReceivingSocket socket, NetMQMessage message, bool dontWait = false);
        public static IEnumerable<byte[]> ReceiveMessages(this IReceivingSocket socket);
        public static string ReceiveString(this IReceivingSocket socket);
        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding);
        public static string ReceiveString(this IReceivingSocket socket, out bool hasMore);
        public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options);
        public static string ReceiveString(this NetMQSocket socket, TimeSpan timeout);
        public static string ReceiveString(this IReceivingSocket socket, bool dontWait, out bool hasMore);
        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, out bool hasMore);
        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, SendReceiveOptions options);
        public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore);
        public static string ReceiveString(this NetMQSocket socket, Encoding encoding, TimeSpan timeout);
        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, bool dontWait, out bool hasMore);
        public static string ReceiveString(this IReceivingSocket socket, Encoding encoding, SendReceiveOptions options, out bool hasMore);
        public static IEnumerable<string> ReceiveStringMessages(this IReceivingSocket socket);
        public static IEnumerable<string> ReceiveStringMessages(this IReceivingSocket socket, Encoding encoding);
        ....
        ....
    }


Here is an example of how one of the above extension methods is implemented, which may help you should you wish to write your own

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



Sending
=====

A <code>NetMQSocket</code> (which all the socket types inherit from) has a single <code>public virtual void Send(ref Msg msg, SendReceiveOptions options)</code> method. But you will likely not actually use this
method. What you will end up using is one the extra extension methods that is available for IOutgoingSocket. These extension methods are shown below, one of these should give you what you want, but if it doesn't
you simply need to write an extra extension method to suit your needs


    public static class OutgoingSocketExtensions
    {
        public static void Send(this IOutgoingSocket socket, byte[] data);
        public static void Send(this IOutgoingSocket socket, byte[] data, int length, SendReceiveOptions options);
        public static void Send(this IOutgoingSocket socket, string message, bool dontWait = false, bool sendMore = false);
        public static void Send(this IOutgoingSocket socket, string message, Encoding encoding, SendReceiveOptions options);
        public static void Send(this IOutgoingSocket socket, byte[] data, int length, bool dontWait = false, bool sendMore = false);
        public static void Send(this IOutgoingSocket socket, string message, Encoding encoding, bool dontWait = false, bool sendMore = false);
        public static void SendMessage(this IOutgoingSocket socket, NetMQMessage message, bool dontWait = false);
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, bool dontWait = false);
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, string message, bool dontWait = false);
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, int length, bool dontWait = false);
        public static IOutgoingSocket SendMore(this IOutgoingSocket socket, string message, Encoding encoding, bool dontWait = false);
        ....
        ....
    }


Here is an example of how one of the above extension methods is implemented, which may help you should you wish to write your own

    public static void Send(this IOutgoingSocket socket, string message, Encoding encoding, SendReceiveOptions options)
    {
        Msg msg = new Msg();
        msg.InitPool(encoding.GetByteCount(message));

        encoding.GetBytes(message, 0, message.Length, msg.Data, 0);

        socket.Send(ref msg, options);

        msg.Close();
    }
