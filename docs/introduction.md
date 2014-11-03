So you are looking for a messaging library, you might got frustrated from WCF or MSMQ (I'm) and heard that ZeroMQ is very fast and then you got here, NetMQ, the .net port of ZeroMQ.
So yes, NetMQ is a messaging library and it is fast, but NetMQ has a bit of learning curve, and hopefully you will get it fast.

## Where to start
ZeroMQ and NetMQ is not just a library that you download, looks at the some code samples and you are done, there is a philosophy behind it and to make good use of it you have to understand it. So the best place to start is with the [ZeroMQ guide]( http://zguide.zeromq.org/page:all), read it, even twice and then come back and here.

## The Zero in ZeroMQ
The philosophy of ZeroMQ start with the zero, the zero is for broker (ZeroMQ is brokerless), zero latency, zero cost (it's free), zero administration.
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
