If you have read the [Introduction](http://netmq.readthedocs.org/en/latest/introduction/) page you would have already seen an example of `ReceiveString()` and `SendString()`, but NetMQ allows us to send and receive more than just strings.

In fact there are quite a few options available to you. Lets go through some of these options, shall we?

---

## Receiving

`IReceivingSocket` (which all the socket types inherit from) has two methods:

``` csharp
void Receive(ref Msg msg);
bool TryReceive(ref Msg msg, TimeSpan timeout);
```

The first blocks indefinitely until a message arrives, and the second allows control over a timeout (which may be zero).

These methods offer great performance by reusing a `Msg` object. However much of the time you want a more convenient method to receive a `string`, `byte[]` and so on. NetMQ has many convenient methods provided as extension methods of `IReceivingSocket` in the `ReceivingSocketExtensions` class:

``` csharp
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
```

(Note the `this IReceivingSocket socket` argument is omitted from all of the above for readability.)

These extension methods should meet most needs, but if they don't it's simple enough to write your own.

Here is an example of how one of the above extension methods is implemented, which may help you should you wish to write your own

``` csharp
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
```

---

## Sending

A `NetMQSocket` (which all the socket types inherit from) has a single send method:

``` csharp
public virtual void Send(ref Msg msg, SendReceiveOptions options)
```

But you will likely not actually use this method. Often it's more convenient to use one of the many extension methods for `IOutgoingSocket`.

These extension methods are shown below, one of these should give you what you want, but if it doesn't you simply need to write an extra extension method to suit your needs.

``` csharp
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
```

Here is an example of how one of the above extension methods is implemented, which may help you should you wish to write your own:

``` csharp
public static void Send(this IOutgoingSocket socket, string message,
                        Encoding encoding, SendReceiveOptions options)
{
    var msg = new Msg();
    msg.InitPool(encoding.GetByteCount(message));
    encoding.GetBytes(message, 0, message.Length, msg.Data, 0);
    socket.Send(ref msg, options);
    msg.Close();
}
```

---

## Further Reading

If you are looking at some of the method signatures, and wondering why/how you should use them, you should read a bit more on the messaging philosophy that NetMQ uses. The [Message](http://netmq.readthedocs.org/en/latest/message/) page has some helpful information around this area.
