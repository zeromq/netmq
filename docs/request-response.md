Request / Response
=====

Request / Response is perhaps the simplest of all the NetMQ socket combinations. That is not to say that `RequestSocket/ResponseSocket` MUST always be used together, that
is no true at all, there are many occassions where you may want to use a particular NetMQ socket with another NetMQ socket. It is just that there are particular
socket arrangements that happen to make a great deal of sense to use together, where `RequestSocket/ResponseSocket` are one such pattern.
<br/>
<br/>
The particular socket combinations that work well together are all covered in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a>. Whilst it may seem a cop out to simply tell you to read more documentation somewhere else, there really is **NO BETTER** documentation on ZeroMQ/NetMQ than you will find in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a>, as it covers well known
patterns that have been proved in the field and are known to work well. 

Anyway we digress, this post is about Request/Response, so lets continue to look at that shall we.




## What Is Request/Response

Request / Response pattern is a configuration of 2 NetMQ sockets working harmoniously together. This combination of sockets are akin to what you might see when you
make a web request. That is you make a request, and you expect a response.

The `RequestSocket / ResponseSocket` are **not asynchronous, and are also blocking**, and you will get an exception if you try and read more messages than are currently available. 

The way you should work with `RequestSocket/ResponseSocket` is as follows:

1. Send a request message from a `RequestSocket`
2. A `ResponseSocket` reads the request message
3. The `ResponseSocket` sends the response message
4. The `RequestSocket` receives the message from the `ResponseSocket`

Believe it or not you have more than likely already seen this example on numerous occassions, as it is the simplest to demonstrate.

Here is a small example where the `RequestSocket/ResponseSocket` code are all in one process, but this could be easily split between 2 processes. We are keeping this as simple
as possible for demonstration purposes.

Example:

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetMQ;

    namespace RequestResponse
    {
        class Program
        {
            static void Main(string[] args)
            {
                using (var context = NetMQContext.Create())
                {
                    using (var responseSocket = context.CreateResponseSocket())
                    {
                        responseSocket.Bind("tcp://*:5555");

                        using (var requestSocket = context.CreateRequestSocket())
                        {
                            requestSocket.Connect("tcp://localhost:5555");
                            Console.WriteLine("requestSocket : Sending 'Hello'");
                            requestSocket.Send("Hello");

                            var message = responseSocket.ReceiveString();

                            Console.WriteLine("responseSocket : Server Received '{0}'", message);

                            Console.WriteLine("responseSocket Sending 'World'");
                            responseSocket.Send("World");

                            message = requestSocket.ReceiveString();
                            Console.WriteLine("requestSocket : Received '{0}'", message);

                            Console.ReadLine();
                        }

                    }
                }
            }
        }
    }


When you run this demo code you should see something like this:
<br/>
<br/>
<br/>
<img src="https://raw.githubusercontent.com/zeromq/netmq/master/docs/Images/RequestResponse.png"/>





## Request/Response Is Blocking

As stated above `RequestSocket/ResponseSocket` is blocking, which means any unexpected calls to `SendXXXX()` / `ReceiveXXXX()` **WILL** result in Exceptions. Here is an exampe of just such an Exception.

In this example we try and call `Send()` twice from the `RequestSocket`

<br/>
<br/>
<img src="https://raw.githubusercontent.com/zeromq/netmq/master/docs/Images/RequestResponse2Sends.png"/>




Or how about this example where we try and call `RecieveString()` twice, but there was only one message sent from the `RequestSocket`


<br/>
<br/>
<img src="https://raw.githubusercontent.com/zeromq/netmq/master/docs/Images/RequestResponse2Receives.png"/>


So be careful what you do with the Request/Response pattern, the devil is in the detail.

