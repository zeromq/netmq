Pub/Pub
=====

NetMQ comes with support for Pub/Sub by way of 2 sockets

+ <code>PublisherSocket</code>
+ <code>SubscriberSocket</code>

Which as usual can be created by using the <code>NetMQContext</code> methods <code>.CreateXXXSocket()</code> methods. Which in this case would be 

+ <code>CreatePublisherSocket()</code>
+ <code>CreateSubscriberSocket()</code>


## Topics

NetMQ allows the use of topics, such that the <code>PublisherSocket</code> may send frame 1 (see the [messages documentation page](https://github.com/zeromq/netmq/blob/master/docs/message.md)) of the message which contains
the topic name followed by the actual message, where you may have something like this

<table CellSpacing="0" Padding="0">
<tr bgcolor="LightGray">
<th>Frame 1</th>
<th>Frame 2</th>
</tr>
<tr>
<td>TopicA</td>
<td>This is a 'TopicA' message</td>
</tr>
</table>

An example of this in code may be something like this (though you could also use the <code>NetMQMessage</code> approach where you add the frames one by one):

    pubSocket.SendMore("TopicA").Send("This is a 'TopicA' message");

The <code>SubscriberSocket</code> may also choose to subscribe to a certain topic only, which it does by passing the topic name into the <code>Subscribe()</code> method of the <code>SubscriberSocket</code>.

An example of this would be as follows:

    subSocket.Subscribe("TopicA");


## How Do You Subscribe To ALL Topics?

It is also possibe for a subscriber to subscribe to all topics from a publishing socket, which means it will recieve (that is providing no messages are dropped see 'Further Considerations'section below)
ALL the messages from the <code>PublisherSocket</code> the <code>SubscriberSocket</code> is connected to. This is easily achieved, all you need to do in the subscriber is to pass an empty string ("") in for the topic name when
calling the <code>subscriberSocket.Subscribe()</code> method. 


## An Example

Time for an example. This example is very simple, and follows these rules. 

+ There is 1 Publisher, who is creating messages, that could be for "TopicA" or "TopicB" (depending on the result of the random number produced)
+ There is a generic Subscriber (the topic name is fed in via the command line arguments) that will subscribe to the incoming topic name

Here is the code:

**Publisher**

    using System;
    using System.Threading;
    using NetMQ;

    namespace Publisher
    {
        class Program
        {
            static void Main(string[] args)
            {
                Random rand = new Random(50);

                using (var context = NetMQContext.Create())
                {
                    using (var pubSocket = context.CreatePublisherSocket())
                    {
                        Console.WriteLine("Publisher socket binding...");
                        pubSocket.Options.SendHighWatermark = 1000;
                        pubSocket.Bind("tcp://localhost:12345");

                        for (var i = 0; i < 100; i++)
                        {
                            var randomizedTopic = rand.NextDouble();
                            if (randomizedTopic > 0.5)
                            {
                                var msg = "TopicA msg-" + i;
                                Console.WriteLine("Sending message : {0}", msg);
                                pubSocket.SendMore("TopicA").Send(msg);
                            }
                            else
                            {
                                var msg = "TopicB msg-" + i;
                                Console.WriteLine("Sending message : {0}", msg);
                                pubSocket.SendMore("TopicB").Send(msg);
                            }

                            Thread.Sleep(500);
                        }
                    }
                }
            }
        }
    }


**Subscriber**

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NetMQ;

    namespace SubscriberA
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

                using (var context = NetMQContext.Create())
                {
                    using (var subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Options.ReceiveHighWatermark = 1000;
                        subSocket.Connect("tcp://localhost:12345");
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



To run this, these 3 BAT file you may be useful, though you will need to change them to suit your code location should you choose to copy this example code into a new set of projects


**RunPubSub.bat**
<br/>
start RunPublisher.bat<br/>
<br/>
<br/>
start RunSubscriber "TopicA"<br/>
start RunSubscriber "TopicB"<br/>
start RunSubscriber "All"<br/>


**RunPublisher.bat**
<br/>
<br/>
cd Publisher\bin\Debug<br/>
Publisher.exe<br/>

**RunSubscriber.bat**
<br/>
<br/>
set "topic=%~1"<br/>
cd Subscriber\bin\Debug<br/>
Subscriber.exe %topic%<br/>



When you run this you should see something like this, where it can be seen that 


<br/>
<img src="https://raw.githubusercontent.com/zeromq/netmq/master/docs/Images/PubSubUsingTopics.png"/>




Other Considerations
=====

**HighWaterMark**


The <code>SendHighWaterMark/ReceiveHighWaterMark</code> options set the high water mark for the specified socket. The high water mark is a hard limit on the maximum number of outstanding messages NetMQ shall queue in memory for any single peer that the specified socket is communicating with.

If this limit has been reached the socket shall enter an exceptional state and depending on the socket type, NetMQ shall take appropriate action such as blocking or dropping sent messages. 

The default <code>SendHighWaterMark/ReceiveHighWaterMark</code> value of zero means "no limit".

You would set these 2 options using the <code>xxxxSocket.Options</code> property as follows:

+  <code>pubSocket.Options.SendHighWatermark = 1000;</code>
+  <code>pubSocket.Options.ReceiveHighWatermark = 1000;</code>


**Slow Subscribers**
<br/>
<br/>
This is covered in the [ZeroMQ guide](http://zguide.zeromq.org/php:chapter5)

**Late Joining Subscribers**
<br/>
<br/>
This is covered in the [ZeroMQ guide](http://zguide.zeromq.org/php:chapter5)
