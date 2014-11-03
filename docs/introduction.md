So you are looking for a messaging library, you might got frustrated from WCF or MSMQ (I know I'm) and heard that ZeroMQ is very fast and then you got here, NetMQ, the .net port of ZeroMQ.
So yes, NetMQ is a messaging library and it is fast, but NetMQ has a bit of learning curve, and hopefully you will get it fast.

## Where to start
ZeroMQ and NetMQ is not just a library that you download, looks at the some code samples and you are done, there is a philosophy behind it and to make good use of it you have to understand it. So the best place to start is with the [ZeroMQ guide]( http://zguide.zeromq.org/page:all), read it, even twice and then come back here.

## The Zero in ZeroMQ
The philosophy of ZeroMQ start with the zero, the zero is for zero broker (ZeroMQ is brokerless), zero latency, zero cost (it's free), zero administration.

More generally, "zero" refers to the culture of minimalism that permeates the project. We add power by removing complexity rather than by exposing new functionality.

## Getting the library

You can get NetMQ library from [nuget](https://nuget.org/packages/NetMQ/).

## First Example
So let's start with some code, the Hello world example of course.
Server:
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
  
Client:
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

## Context

## Multithreading

## Patterns

## Sending and Receiving

## High Watermark

## Multipart messages


