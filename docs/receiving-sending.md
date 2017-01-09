如果你前面有讀過 [Introduction](http://netmq.readthedocs.org/en/latest/introduction/)，那你應該有看到範例中有使用到ReceiveString()和SendString()了，但NetMQ讓我們不只可以傳送和接收字串。

實際上有不少可以使用的選項，讓我們來看其中的一部份吧！

---

## Receiving

`IReceivingSocket`(所有socket皆繼承自此介面)有兩個函式：

    :::csharp
    void Receive(ref Msg msg);

    bool TryReceive(ref Msg msg, TimeSpan timeout);

第一個函式會永遠阻塞直到訊息到達，第二個讓我們提供一個逾時時間(可能是零)。

這些函式依靠Msg物件的重覆使用提供我們很高的效能。然而大多時間你會想要更方便的函式來幫你接收`string`，`byte[]`等型別，NetMQ在`ReceivingSocketExtensions`類別中提供了很多`IReceivingSocket`型別的方便函式：

    :::csharp
    // Receiving byte[]
    byte[] ReceiveFrameBytes()
    byte[] ReceiveFrameBytes(out bool more)
    bool TryReceiveFrameBytes(out byte[] bytes)
    bool TryReceiveFrameBytes(out byte[] bytes, out bool more)
    bool TryReceiveFrameBytes(TimeSpan timeout, out byte[] bytes)
    bool TryReceiveFrameBytes(TimeSpan timeout, out byte[] bytes, out bool more)
    List<byte[]> ReceiveMultipartBytes()
    void ReceiveMultipartBytes(ref List<byte[]> frames)
    bool TryReceiveMultipartBytes(ref List<byte[]> frames)
    bool TryReceiveMultipartBytes(TimeSpan timeout, ref List<byte[]> frames)

    // Receiving strings
    string ReceiveFrameString()
    string ReceiveFrameString(out bool more)
    string ReceiveFrameString(Encoding encoding)
    string ReceiveFrameString(Encoding encoding, out bool more)
    bool TryReceiveFrameString(out string frameString)
    bool TryReceiveFrameString(out string frameString, out bool more)
    bool TryReceiveFrameString(Encoding encoding, out string frameString)
    bool TryReceiveFrameString(Encoding encoding, out string frameString, out bool more)
    bool TryReceiveFrameString(TimeSpan timeout, out string frameString)
    bool TryReceiveFrameString(TimeSpan timeout, out string frameString, out bool more)
    bool TryReceiveFrameString(TimeSpan timeout, Encoding encoding, out string frameString)
    bool TryReceiveFrameString(TimeSpan timeout, Encoding encoding, out string frameString, out bool more)
    List<string> ReceiveMultipartStrings()
    List<string> ReceiveMultipartStrings(Encoding encoding)
    bool TryReceiveMultipartStrings(ref List<string> frames)
    bool TryReceiveMultipartStrings(Encoding encoding, ref List<string> frames)
    bool TryReceiveMultipartStrings(TimeSpan timeout, ref List<string> frames)
    bool TryReceiveMultipartStrings(TimeSpan timeout, Encoding encoding, ref List<string> frames)

    // Receiving NetMQMessage
    NetMQMessage ReceiveMultipartMessage()
    bool TryReceiveMultipartMessage(ref NetMQMessage message)
    bool TryReceiveMultipartMessage(TimeSpan timeout, ref NetMQMessage message)

    // Receiving signals
    bool ReceiveSignal()
    bool TryReceiveSignal(out bool signal)
    bool TryReceiveSignal(TimeSpan timeout, out bool signal)

    // Skipping frames
    void SkipFrame()
    void SkipFrame(out bool more)
    bool TrySkipFrame()
    bool TrySkipFrame(out bool more)
    bool TrySkipFrame(TimeSpan timeout)
    bool TrySkipFrame(TimeSpan timeout, out bool more)

注意為了可讀性`this IReceivingSocket socket`參數被省略掉了。

這些擴充函式應符合大多數的需求，如果沒有的話你也可以很簡單的建立自己需要的。

這裡是上述擴充函式之一實作的方式，可以幫助你建立自己的：

    :::csharp
    public static string ReceiveFrameString(this IReceivingSocket socket, Encoding encoding, out bool more)
    {
        var msg = new Msg();
        msg.InitEmpty();
        socket.Receive(ref msg);
        more = msg.HasMore;
        var str = msg.Size > 0
            ? encoding.GetString(msg.Data, 0, msg.Size)
            : string.Empty;
        msg.Close();
        return str;
    }

---

## Sending

一個`NetMQSocket`(所有socket皆繼承至此)有一個send函式。

    :::csharp
    public virtual void Send(ref Msg msg, SendReceiveOptions options)

如果你不想使用這個函式，也可以用為了`IOutgoingSocket`建立的其它方便的擴充函式。

下面列出這些擴充函式，應該夠你使用，若是不足也可以自行建立。

    :::csharp
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

這裡是上述擴充函式之一實作的方式，可以幫助你建立自己的：

    :::csharp
    public static void Send(this IOutgoingSocket socket, string message,
                            Encoding encoding, SendReceiveOptions options)
    {
        var msg = new Msg();
        msg.InitPool(encoding.GetByteCount(message));
        encoding.GetBytes(message, 0, message.Length, msg.Data, 0);
        socket.Send(ref msg, options);
        msg.Close();
    }

---

## Further Reading

If you are looking at some of the method signatures, and wondering why/how you should use them, you should read a bit more on the messaging philosophy that NetMQ uses. The [Message](http://netmq.readthedocs.org/en/latest/message/) page has some helpful information around this area.
