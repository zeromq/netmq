Message Structure
===

So if you have come here after looking at some of the introductory material, you may have come across an example or two, maybe even a "hello world" example resembling:

    :::csharp
    using (var server = new ResponseSocket("@tcp://127.0.0.1:5556"))
    using (var client = new RequestSocket(">tcp://127.0.0.1:5556"))
    {
        client.Send("Hello");

        string fromClientMessage = server.ReceiveFrameString();

        Console.WriteLine("From Client: {0}", fromClientMessage);

        server.SendFrame("Hi Back");

        string fromServerMessage = client.ReceiveFrameString();

        Console.WriteLine("From Server: {0}", fromServerMessage);

        Console.ReadLine();
    }

Where you may have noticed (or perhaps not) that the NetMQ socket(s) have a `ReceiveFrameString()` method. This is good, and extremely useful, but you may be fooled into thinking this is what you should be using all the time.

Truth is ZeroMQ, and therefore NetMQ are really frame based, which implies some form of protocol. Some of you may balk at this prospect, and may curse, and think damm it, I am not a protocol designer I was not expecting to get my hands that dirty.

While it is true that if you wish to come up with some complex and elaborate architecture you would be best of coming up with a nice protocol, thankfully you will not need to do this all the time. This is largely down to ZeroMQ/NetMQ's clever sockets that abstract away a lot of that from you, and the way in which you can treat the sockets as building blocks to build complex architecture (think lego).

One precanned example of this is the [RouterSocket](router-dealer.md) which makes very clever use of frames for you out of the box. Where it effectively onion skins the current message with the senders return address, so that when it gets a message back (say from a worker socket), it can use that frame information again to obtain the correct return address and send it back to the correct socket.

So that is one inbuilt use of frames that you should be aware of, but frames are not limited to [RouterSocket](router-dealer.md), you can use them yourself for all sorts of things, here are some examples:

+ You may decide to have `frame[0]` denote the specific message type of following frame(s).
  This allows receivers to discard message types they are disinterested in without wasting time deserialising a message they do not care about anyway.
  ZeroMQ/NetMQ uses this approach in its [Pub-Sub sockets](pub-sub.md), and you can replicate or extend this idea.
+ You may decide to use `frame[0]` as some sort of command, `frame[1]` and some sort of parameter and have `frame[2]` as the message payload (where it may contain some serialized object).

These are just some examples. You can use frames however you wish really, although some socket types expect or produce certain frame structures.

When you work with multipart messages (frames) you must send/receive all the parts of the message you want to work with.

There is also an inbuilt concept of "more" which you can integrate for. We will see some examples of this in just a moment.


## Creating multipart messages

Creating multipart messages is fairly simple, and there are two ways of doing so.

### Building a message object

You may build a `NetMQMessage` object and add frame data directly into it via one of the many `Append(...)` method overloads. There are overloads for appending `Blob`, `NetMQFrame`, `byte[]`, `int`, `long` and `string`.

Here is a simple example where we create a new message containing two frames, each containing string values:

    :::csharp
    var message = new NetMQMessage();
    message.Append("IAmFrame0");
    message.Append("IAmFrame1");
    server.SendMessage(message);

### Sending frame by frame

Another way of sending multipart messages is to use `SendMoreFrame` extension methods. This doesn't have as many overloads as `SendMessage` but it allows you to send `byte[]` and `string` data quite easily. Here is an example with identical behaviour to that we have just seen:

    :::csharp
    server.SendMoreFrame("IAmFrame0")
          .SendFrame("IAmFrame1");

To send more than two frames, chain together multiple `SendMoreFrame` calls. Just be sure to always end the chain with `SendFrame`!


## Reading multipart messages

Reading multiple frames can also be done in two ways.

### Receiving individual frames

You can read frames from the socket one by one. The out-param `more` will tell you whether or not this is the last frame in the message.

You may use the NetMQ convenience `ReceiveFrameString(out more)` method multiple times, where you would need to know if there was more than one message part to read, which you would need to track in a bool variable. This is shown below

    :::csharp
    // server sends a message with two frames
    server.SendMoreFrame("A")
          .SendFrame("Hello");

    // client receives all frames in the message, one by one
    bool more = true;
    while (more)
    {
        string frame = client.ReceiveFrameString(out more);
        Console.WriteLine("frame={0}", frame);
        Console.WriteLine("more={0}", more);
    }

This loop would execute twice. The first time, `more` will be set `true`. The second time around, `false`. The output would be:

    :::text
    frame=A
    more=true
    frame=Hello
    more=false

### Reading entire messages

An easier way is to use the `ReceiveMultipartMessage()` method which gives you an object containing all the frames of the message.

    :::csharp
    NetMQMessage message = client.ReceiveMultipartMessage();
    Console.WriteLine("message.FrameCount={0}", message.FrameCount);
    Console.WriteLine("message[0]={0}", message[0].ConvertToString());
    Console.WriteLine("message[1]={0}", message[1].ConvertToString());

The output of which would be:

    :::text
    message.FrameCount=2
    message[0]=A
    message[1]=Hello

Other approaches exist, including:

    :::csharp
    IEnumerable<string> framesAsStrings = client.ReceiveMultipartStrings();

    IEnumerable<byte[]> framesAsByteArrays = client.ReceiveMultipartBytes();


## A Full Example

Just to solidify this information here is a complete example showing everything we have discussed above:

    :::csharp
    using (var server = new ResponseSocket("@tcp://127.0.0.1:5556"))
    using (var client = new RequestSocket(">tcp://127.0.0.1:5556"))
    {
        // client sends message consisting of two frames
        Console.WriteLine("Client sending");
        client.SendMoreFrame("A").SendFrame("Hello");

        // server receives frames
        bool more = true;
        while (more)
        {
            string frame = server.ReceiveFrameString(out more);
            Console.WriteLine("Server received frame={0} more={1}",
                frame, more);
        }

        Console.WriteLine("================================");

        // server sends message, this time using NetMqMessage
        var msg = new NetMQMessage();
        msg.Append("From");
        msg.Append("Server");

        Console.WriteLine("Server sending");
        server.SendMultipartMessage(msg);

        // client receives the message
        msg = client.ReceiveMultipartMessage();
        Console.WriteLine("Client received {0} frames", msg.FrameCount);

        foreach (var frame in msg)
            Console.WriteLine("Frame={0}", frame.ConvertToString());

        Console.ReadLine();
    }

Which when run will give you some output like this:

    :::text
    Client sending
    Server received frame=A more=true
    Server received frame=Hello more=false
    ================================
    Server sending
    Client received 2 frames
    Frame=From
    Frame=Server
