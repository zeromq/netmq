XPub / XSub
=====


<i>
One of the problems you will hit as you design larger distributed architectures is discovery. That is, how do pieces know about each other? It's especially difficult if pieces come and go, so we call this the "dynamic discovery problem".
<br/>
<br/>
There are several solutions to dynamic discovery. The simplest is to entirely avoid it by hard-coding (or configuring) the network architecture so discovery is done by hand. That is, when you add a new piece, you reconfigure the network to know about it.
<br/>
<br/>
<img src="https://github.com/imatix/zguide/raw/master/images/fig12.png"/>
<br/>
<br/>
In practice, this leads to increasingly fragile and unwieldy architectures. Let's say you have one publisher and a hundred subscribers. You connect each subscriber to the publisher by configuring a publisher endpoint in each subscriber. That's easy. Subscribers are dynamic; the publisher is static. Now say you add more publishers. Suddenly, it's not so easy any more. If you continue to connect each subscriber to each publisher, the cost of avoiding dynamic discovery gets higher and higher.
<br/>
<br/>
<img src="https://github.com/imatix/zguide/raw/master/images/fig13.png"/>
<br/>
<br/>
There are quite a few answers to this, but the very simplest answer is to add an intermediary; that is, a static point in the network to which all other nodes connect. In classic messaging, this is the job of the message broker. ZeroMQ doesn't come with a message broker as such, but it lets us build intermediaries quite easily.
<br/>
<br/>
You might wonder, if all networks eventually get large enough to need intermediaries, why don't we simply have a message broker in place for all applications? For beginners, it's a fair compromise. Just always use a star topology, forget about performance, and things will usually work. However, message brokers are greedy things; in their role as central intermediaries, they become too complex, too stateful, and eventually a problem.
<br/>
<br/>
It's better to think of intermediaries as simple stateless message switches. A good analogy is an HTTP proxy; it's there, but doesn't have any special role. Adding a pub-sub proxy solves the dynamic discovery problem in our example. We set the proxy in the "middle" of the network. The proxy opens an XSUB socket, an XPUB socket, and binds each to well-known IP addresses and ports. Then, all other processes connect to the proxy, instead of to each other. It becomes trivial to add more subscribers or publishers.
<br/>
<br/>
We need XPUB and XSUB sockets because ZeroMQ does subscription forwarding from subscribers to publishers. XSUB and XPUB are exactly like SUB and PUB except they expose subscriptions as special messages. The proxy has to forward these subscription messages from subscriber side to publisher side, by reading them from the XSUB socket and writing them to the XPUB socket. This is the main use case for XSUB and XPUB.
</i>
<br/>
<br/>
The text above is taken from <a href="ZeroMQ guide - dynamix discovery problem" target="_blank">http://zguide.zeromq.org/page:all#The-Dynamic-Discovery-Problem</a>, we just could not have said it better ourselves, and this library is really part of ZeroMQ anyway, so please forgive us.


## An Example

So now that we have gone through why you would use XPub/XSub, lets now look at an example. This example sticks
very closely to the orginal <a href="ZeroMQ guide - dynamix discovery problem" target="_blank">http://zguide.zeromq.org/page:all#The-Dynamic-Discovery-Problem</a>, and is broken
down into 3 main components, which are:

+ Publisher
+ Intermediary
+ Subscriber

Here is the code for these 3 components:

**Publisher**

It can be seen that the `PublisherSocket` connnects to the `XSubscriberSocket` address

    using System;
    using NetMQ;

    namespace XPubXSub.Publisher
    {
        class Program
        {
            static void Main(string[] args)
            {

                Random rand = new Random(50);

                const string xsubAddress = "tcp://127.0.0.1:5678";


                using (var context = NetMQContext.Create())
                {
                    using (var pubSocket = context.CreatePublisherSocket())
                    {
                        Console.WriteLine("Publisher socket binding...");
                        pubSocket.Options.SendHighWatermark = 1000;
                        pubSocket.Connect(xsubAddress);



                        while(true)
                        {
                            var randomizedTopic = rand.NextDouble();
                            if (randomizedTopic > 0.5)
                            {
                                var msg = "TopicA msg-" + randomizedTopic;
                                Console.WriteLine("Sending message : {0}", msg);
                                pubSocket.SendMore("TopicA").Send(msg);
                            }
                            else
                            {
                                var msg = "TopicB msg-" + randomizedTopic;
                                Console.WriteLine("Sending message : {0}", msg);
                                pubSocket.SendMore("TopicB").Send(msg);
                            }
                        }
                    }
                }

            }
        }
    }



**Intermediary**

The intermediary (the process that owns the `XPublisherSocket` / `XSubscriberSocket`) is responsible for relaying
the messages as follows:

+ From the `XPublisherSocket` to the `XSubscriberSocket`
+ From the `XSubscriberSocket` to the `XPublisherSocket`

This would be done with the use of the NetMQ `Poller`, but there is a better way, which is to use the NetMQ `Proxy` which allows
you to specify 2 sockets, which you wish to proxy between. The NetMQ `Proxy` will take care of sending the front end messages to the back end, and vice versa.

This is the appoach we have shown in this example. Anyway enough pyscho bablble, here is the code:

    using System;
    using System.Threading.Tasks;
    using NetMQ;

    namespace XPubXSub.Intermediary
    {
        class Program
        {
            static void Main(string[] args)
            {

                const string xpubAddress = "tcp://127.0.0.1:1234";
                const string xsubAddress = "tcp://127.0.0.1:5678";

                using (var context = NetMQContext.Create())
                {
                    using (var xpubSocket = context.CreateXPublisherSocket())
                    {
                        xpubSocket.Bind(xpubAddress);

                        using (var xsubSocket = context.CreateXSubscriberSocket())
                        {

                            xsubSocket.Bind(xsubAddress);

                            Console.WriteLine("Intermediary started, and waiting for messages");


                            //Use the Proxy class to proxy between frontend / backend
                            NetMQ.Proxy proxy = new Proxy(xsubSocket, xpubSocket,null);
                            Task.Factory.StartNew(proxy.Start);

                            Console.ReadLine();
                        }

                    }
                }
            }
        }
    }



**Subscriber**

It can be seen that the `SubscriberSocket` connnects to the `XPublisherSocket` address

    using System;
    using System.Collections.Generic;
    using NetMQ;

    namespace XPubXSub.Subscriber
    {
        class Program
        {

            public static List<string> allowableCommandLineArgs = new List<string>();

            static Program()
            {
                allowableCommandLineArgs.Add("TopicA");
                allowableCommandLineArgs.Add("TopicB");
                allowableCommandLineArgs.Add("All");
            }

            static void PrintUsageAndExit()
            {
                Console.WriteLine("Subscriber is expected to be started with either 'TopicA', 'TopicB' or 'All'");
                Console.ReadLine();
                Environment.Exit(-1);
            }

            static void Main(string[] args)
            {

                if (args.Length != 1)
                {
                    PrintUsageAndExit();
                }

                if (!allowableCommandLineArgs.Contains(args[0]))
                {
                    PrintUsageAndExit();
                }

                string topic = args[0] == "All" ? "" : args[0];
                Console.WriteLine("Subscriber started for Topic : {0}", topic);

                const string xpubPortAddress = "tcp://127.0.0.1:1234";

                using (var context = NetMQContext.Create())
                {
                    using (var subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Options.ReceiveHighWatermark = 1000;
                        subSocket.Connect(string.Format(xpubPortAddress));
                        subSocket.Subscribe(topic);
                        Console.WriteLine("Subscriber socket connecting...");

                        while (true)
                        {
                            string messageTopicReceived = subSocket.ReceiveString();
                            string messageReceived = subSocket.ReceiveString();
                            Console.WriteLine(messageReceived);
                        }



                    }
                }
            }
        }
    }


When you run this, it should look something like this (depending on how many subscribers you have, and what topics they are subsribed too, of course now that we
have a nice topology you could indeed have more publishers too)

![](Images/XPubXSubDemo.png)


To run this, these 4 BAT file you may be useful, though you will need to change them to suit your code location should you choose to copy this example code into a new set of projects


**RunXPubXSub.bat**
<br/>
start RunXPubXSubIntermediary.bat<br/>
start RunXPublisher.bat<br/>
<br/>
<br/>
start RunXSubscriber "TopicA"<br/>
start RunXSubscriber "TopicB"<br/>
<br/>
<br/>

**RunXPublisher.bat**
<br/>
cd XPubXSub.Publisher\bin\Debug<br/>
XPubXSub.Publisher.exe<br/>
<br/>
<br/>

**RunXPubXSubIntermediary.bat**
<br/>
cd XPubXSub.Intermediary\bin\Debug<br/>
XPubXSub.Intermediary.exe<br/>
<br/>
<br/>

**RunXSubscriber.bat**
<br/>
set "topic=%~1"<br/>
<br/>
<br/>
cd XPubXSub.Subscriber\bin\Debug<br/>
XPubXSub.Subscriber.exe %topic%<br/>
