Introduction
=====

So you are looking for a messaging library, you might have become frustrated with WCF or MSMQ (I know I'm) and heard that ZeroMQ is very fast and then you got here, NetMQ, the .net port of ZeroMQ.

So yes, NetMQ is a messaging library and it is fast, but NetMQ has a bit of learning curve, and hopefully you will get it fast.

Where to start
=====
ZeroMQ and NetMQ is not just a library that you download, looks at the some code samples and you are done, there is a philosophy behind it and to make good use of it you have to understand it. So the best place to start is with the [ZeroMQ guide]( http://zguide.zeromq.org/page:all), read it, even twice and then come back here.

The Zero in ZeroMQ
=====
The philosophy of ZeroMQ start with the zero, the zero is for zero broker (ZeroMQ is brokerless), zero latency, zero cost (it's free), zero administration.

More generally, "zero" refers to the culture of minimalism that permeates the project. We add power by removing complexity rather than by exposing new functionality.

Getting the library
=====

You can get NetMQ library from [nuget](https://nuget.org/packages/NetMQ/).

First Example
=====
So let's start with some code, the Hello world example of course.

**Server:**
    
    static void Main(string[] args)
    {
        using (var context = NetMQContext.Create())
        {
            using (var server = context.CreateResponseSocket())
            {
                server.Bind("tcp://*:5555");

                while (true)
                {
                    var message = server.ReceiveString();

                    Console.WriteLine("Received {0}", message);

                    // processing the request
                    Thread.Sleep(100);

                    Console.WriteLine("Sending World");
                    server.Send("World");
                }
            }
        }
    }
    
The server create a socket of type response (you can read more on the reqeust-response chapter), bind it to port 5555 and wait for messages. You can also see that we have zero configuration, we just sending strings. NetMQ can send much more than strings, but NetMQ doesn't come with any serialization feature and you have to do it by hand, but you will learn some cool tricks for that (Multi part messages).
  
**Client:**
    
    static void Main(string[] args)
    {
        using (var context = NetMQContext.Create())
        {
            using (var client = context.CreateRequestSocket())
            {
                client.Connect("tcp://localhost:5555");

                for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine("Sending Hello");
                    client.Send("Hello");
                        
                    var message = client.ReceiveString();                        
                    Console.WriteLine("Received {0}", message);
                }
            }
        }
    }

The client create a socket of type response, connect and start sending messages.

Both the sending and receive methods are blocking, for the receive it is simple, if there are no messages the method will block, for sending it is more complicated and depends on the socket type. In request socket type if the high watermark is reached or no peer is connected the method will block.

You can however call receive or send with the DontWait flag to avoid the waiting, make sure to wrap the send or receive with try and catch the AgainException, like so:

    try
    {
        var message = client.ReceiveString(SendReceiveOptions.DontWait);
        Console.WriteLine("Received {0}", message);
    }
    catch (AgainException ex)
    {
        Console.WriteLine(ex);                        
    }

Context
=====

Multithreading
=====

Patterns
=====
[ZeroMQ](http://zguide.zeromq.org/page:all) (and therefore NetMQ) is all about patterns, and building blocks. The [ZeroMQ Guide](http://zguide.zeromq.org/page:all) covers everything you need to know to help you with these patterns. You should make sure you read the following sections before you attempt to start work with NetMQ.

+ [Chapter 2 - Sockets and Patterns] (http://zguide.zeromq.org/page:all#Chapter-Sockets-and-Patterns)
+ [Chapter 3 - Advanced Request-Reply Patterns] (http://zguide.zeromq.org/page:all#Chapter-Advanced-Request-Reply-Patterns)
+ [Chapter 4 - Reliable Request-Reply Patterns] (http://zguide.zeromq.org/page:all#Chapter-Reliable-Request-Reply-Patterns)
+ [Chapter 5 - Advanced Pub-Sub Patterns] (http://zguide.zeromq.org/page:all#Chapter-Advanced-Pub-Sub-Patterns)


NetMQ also has some examples of a few of these patterns written using the NetMQ APIs. Should you find the pattern you are looking for in the [ZeroMQ Guide](http://zguide.zeromq.org/page:all) it should be fairly easy to translate that into NetMQ usage.

Here are some links to the patterns that are available within the NetMQ codebase:

+ [Borkerless Reliability Pattern - Freelance Model one] (https://github.com/zeromq/netmq/tree/master/src/Samples/Brokerless%20Reliability%20(Freelance%20Pattern)/Model%20One)
+ [Load Balancer Pattern] (https://github.com/zeromq/netmq/tree/master/src/Samples/Load%20Balancing%20Pattern)
+ [Lazy Pirate Pattern] (https://github.com/zeromq/netmq/tree/master/src/Samples/Pirate%20Pattern/Lazy%20Pirate)
+ [Simple Pirate Pattern] (https://github.com/zeromq/netmq/tree/master/src/Samples/Pirate%20Pattern/Simple%20Pirate)

For other patterns, the [ZeroMQ Guide](http://zguide.zeromq.org/page:all) will be your first port of call


Sending and Receiving
=====



Options
=====
NetMQ comes with several options that will effect how things work. 

Depending on the type of sockets you are using, or the topology you are attempting to create, you may find that you need to set some NeroMQ options. In NetMQ this is done using the xxxxSocket.Options property.

Here is a listing of the available properties that you may set on a xxxxSocket. It is hard to say exactly which of these values you may need to set, as that obviously depends entirely on what you are trying to achieve. All I can do is list the options, and make you aware of them. So here they are

+ Affinity  
+ BackLog  
+ CopyMessages
+ DelayAttachOnConnect
+ Endian
+ GetLastEndpoint
+ IPv4Only
+ Identity
+ Linger
+ MaxMsgSize
+ MulticastHops
+ MulticastRate
+ MulticastRecoveryInterval
+ ReceiveHighWaterMark
+ ReceiveMore
+ ReceiveTimeout
+ ReceiveBuffer
+ ReconnectInterval
+ ReconnectIntervalMax
+ SendHighWaterMark
+ SendTimeout
+ SendBuffer
+ TcpAcceptFilter
+ TcpKeepAlive
+ TcpKeepaliveCnt
+ TcpKeepaliveIdle
+ TcpKeepaliveInterval
+ XPubVerbos

We will not be covering all of these here, but shall instead cover them in the areas where they are used. For now just be aware that you have read something in the [ZeroMQ guide]( http://zguide.zeromq.org/page:all) that mentions some option, that this is most likely the place you will need to set it/read from it


Multipart messages
=====

ZeroMQ/NetMQ work on the concept of frames, as such most messages are considered to be made up of one or more frames. NetMQ provides some convenience methods to allow you to send string messages. You should however, familiarise yourself with the the idea of multiple frames and how they work.

This is covered in much more detail in the [Message](https://github.com/zeromq/netmq/blob/master/docs/router.md) documentation page

