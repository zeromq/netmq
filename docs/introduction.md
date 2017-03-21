Introduction
=====

So you are looking for a messaging library, you might have become frustrated with WCF or MSMQ (we know we have been there too) and heard that ZeroMQ is very fast and then you got here, NetMQ, the .NET port of ZeroMQ (also known as ØMQ).

So yes, NetMQ is a messaging library and it is fast, but NetMQ has a bit of learning curve. Hopefully you will pick it up quickly.


## Where to start

ZeroMQ and NetMQ is not just a library that you download, look at the some code samples and then you are done. There is a philosophy behind it and to make good use of it you have to understand it. So the best place to start is with the [ZeroMQ guide](http://zguide.zeromq.org/page:all). Read it, even twice, and then come back here.


## The Zero in ZeroMQ

The philosophy of ZeroMQ starts with the _zero_. The zero is for zero broker (ZeroMQ is brokerless), zero latency, zero cost (it's free), and zero administration.

More generally, "zero" refers to the culture of minimalism that permeates the project. We add power by removing complexity rather than by exposing new functionality.


## Getting the library

You can get NetMQ library from [NuGet](https://nuget.org/packages/NetMQ/).


## Sending and receiving

Since NetMQ is all about the sockets, it is only natural that one would expect to able to send/receive. Since this is such a common area of NetMQ, there is a dedicated documentation page on [receiving and sending](receiving-sending.md).


## First example

So let's start with some code, the "Hello world" example (of course).

### Server

    :::csharp
    using (var server = new ResponseSocket())
    {
        server.Bind("tcp://*:5555");

        while (true)
        {
            var message = server.ReceiveFrameString();

            Console.WriteLine("Received {0}", message);

            // processing the request
            Thread.Sleep(100);

            Console.WriteLine("Sending World");
            server.SendFrame("World");
        }
    }

The server creates a socket of type response (you can read more on the [request-response](request-response.md) chapter), binds it to port 5555 and then waits for messages.

You can also see that we have zero configuration, we are just sending strings. NetMQ can send much more than strings, but NetMQ doesn't come with any serialization feature and you have to do it by hand, but you will learn some cool tricks for that below ([Multipart messages](#multipart-messages)).

### Client

    :::csharp
    using (var client = new RequestSocket())
    {
        client.Connect("tcp://localhost:5555");

        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("Sending Hello");
            client.SendFrame("Hello");

            var message = client.ReceiveFrameString();
            Console.WriteLine("Received {0}", message);
        }
    }

The client create a socket of type request, connect and start sending messages.

Both the `Send` and `Receive` methods are blocking (by default). For the receive it is simple: if there are no messages the method will block. For sending it is more complicated and depends on the socket type. For request sockets, if the high watermark is reached or no peer is connected the method will block.

You can however call `TrySend` and `TryReceive` to avoid the waiting. The operation returns `false` if it would have blocked.

    :::csharp
    string message;
    if (client.TryReceiveFrameString(out message))
        Console.WriteLine("Received {0}", message);
    else
        Console.WriteLine("No message received");


## Bind vs Connect

In the above you may have noticed that the server used `Bind` while the client used `Connect`. Why is this, and what is the difference?

ZeroMQ creates queues per underlying connection. If your socket is connected to three peer sockets, then there are three messages queues behind the scenes.

With `Bind`, you allow peers to connect to you, thus you don't know how many peers there will be in the future and you cannot create the queues in advance. Instead, queues are created as individual peers connect to the bound socket.

With `Connect`, ZeroMQ knows that there's going to be at least a single peer and thus it can create a single queue immediately. This applies to all socket types except ROUTER, where queues are only created after the peer we connect to has acknowledge our connection.

Consequently, when sending a message to bound socket with no peers, or a ROUTER with no live connections, there's no queue to store the message to.

### When should I use bind and when connect?

As a general rule use bind from the most stable points in your architecture, and use connect from dynamic components with volatile endpoints. For request/reply, the service provider might be point where you bind and the client uses connect. Just like plain old TCP.

If you can't figure out which parts are more stable (i.e. peer-to-peer), consider a stable device in the middle, which all sides can connect to.

You can read more about this at the [ZeroMQ FAQ](http://zeromq.org/area:faq) under the _"Why do I see different behavior when I bind a socket versus connect a socket?"_ section.


## Multipart messages

ZeroMQ/NetMQ work on the concept of frames, as such most messages are considered to be made up of one or more frames. NetMQ provides some convenience methods to allow you to send string messages. You should however, familiarise yourself with the the idea of multiple frames and how they work.

This is covered in much more detail in the [Message](message.md) documentation page.


## Patterns

ZeroMQ (and therefore NetMQ) is all about patterns and building blocks. The <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> covers everything you need to know to help you with these patterns. You should make sure you read the following sections before you attempt to start work with NetMQ.

+ <a href="http://zguide.zeromq.org/page:all#Chapter-Sockets-and-Patterns" target="_blank">Chapter 2 - Sockets and Patterns</a>
+ <a href="http://zguide.zeromq.org/page:all#Chapter-Advanced-Request-Reply-Patterns" target="_blank">Chapter 3 - Advanced Request-Reply Patterns</a>
+ <a href="http://zguide.zeromq.org/page:all#Chapter-Reliable-Request-Reply-Patterns" target="_blank">Chapter 4 - Reliable Request-Reply Patterns</a>
+ <a href="http://zguide.zeromq.org/page:all#Chapter-Advanced-Pub-Sub-Patterns" target="_blank">Chapter 5 - Advanced Pub-Sub Patterns</a>


NetMQ also has some examples of a few of these patterns written using the NetMQ APIs. Should you find the pattern you are looking for in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> it should be fairly easy to translate that into NetMQ usage.

Here are some links to the patterns that are available within the NetMQ codebase:

+ <a href="https://github.com/NetMQ/Samples/blob/master/src/Brokerless%20Reliability%20(Freelance%20Pattern)/Model%20One" target="_blank">Brokerless Reliability Pattern - Freelance Model one</a>
+ <a href="https://github.com/NetMQ/Samples/blob/master/src/Load%20Balancing%20Pattern" target="_blank">Load Balancer Patterns</a>
+ <a href="https://github.com/NetMQ/Samples/blob/master/src/Pirate%20Pattern/Lazy%20Pirate" target="_blank">Lazy Pirate Pattern</a>
+ <a href="https://github.com/NetMQ/Samples/blob/master/src/Pirate%20Pattern/Simple%20Pirate" target="_blank">Simple Pirate Pattern</a>

For other patterns, the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a>
will be your first port of call

ZeroMQ patterns are implemented by pairs of sockets of particular types. In other words, to understand ZeroMQ patterns you need to understand socket types and how they work together. Mostly, this just takes study; there is little that is obvious at this level.

The built-in core ZeroMQ patterns are:

+ [**Request-reply**](request-response.md), which connects a set of clients to a set of services. This is a remote procedure call and task distribution pattern.
+ [**Pub-sub**](pub-sub.md), which connects a set of publishers to a set of subscribers. This is a data distribution pattern.
+ **Pipeline**, which connects nodes in a fan-out/fan-in pattern that can have multiple steps and loops. This is a parallel task distribution and collection pattern.
+ **Exclusive pair**, which connects two sockets exclusively. This is a pattern for connecting two threads in a process, not to be confused with "normal" pairs of sockets.

These are the socket combinations that are valid for a connect-bind pair (either side can bind):

+ `PublisherSocket` and `SubscriberSocket`
+ `RequestSocket` and `ResponseSocket`
+ `RequestSocket`  and `RouterSocket`
+ `DealerSocket` and `ResponseSocket`
+ `DealerSocket` and `RouterSocket`
+ `DealerSocket` and `DealerSocket`
+ `RouterSocket` and `RouterSocket`
+ `PushSocket` and `PullSocket`
+ `PairSocket` and `PairSocket`

Any other combination will produce undocumented and unreliable results, and future versions of ZeroMQ will probably return errors if you try them. You can and will, of course, bridge other socket types via code, i.e., read from one socket type and write to another.


## Options

NetMQ comes with several options that will effect how things work.

Depending on the type of sockets you are using, or the topology you are attempting to create, you may find that you need to set some ZeroMQ options. In NetMQ this is done using the `NetMQSocket.Options` property.

Here is a listing of the available properties that you may set on a `NetMQSocket`. It is hard to say exactly which of these values you may need to set, as that obviously depends entirely on what you are trying to achieve. All I can do is list the options, and make you aware of them. So here they are:

+ `Affinity`
+ `BackLog`
+ `CopyMessages`
+ `DelayAttachOnConnect`
+ `Endian`
+ `GetLastEndpoint`
+ `IPv4Only`
+ `Identity`
+ `Linger`
+ `MaxMsgSize`
+ `MulticastHops`
+ `MulticastRate`
+ `MulticastRecoveryInterval`
+ `ReceiveHighWaterMark`
+ `ReceiveMore`
+ `ReceiveBuffer`
+ `ReconnectInterval`
+ `ReconnectIntervalMax`
+ `SendHighWaterMark`
+ `SendTimeout`
+ `SendBuffer`
+ `TcpAcceptFilter`
+ `TcpKeepAlive`
+ `TcpKeepaliveIdle`
+ `TcpKeepaliveInterval`
+ `XPubVerbose`

We will not be covering all of these here, but shall instead cover them in the areas where they are used. For now just be aware that if you have read something in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> that mentions some option, that this is most likely the place you will need to set it/read from it.
