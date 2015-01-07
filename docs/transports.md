Transport Protocols
=====

NetMQ comes with support for 3 main protocols

+ TCP
+ InProc
+ PGM

Each of these is discussed below



TCP
=====

TCP is the most common protocol that gets used, as such most of the examples shown will use TCP. Here is another trivial
example

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetMQ;

    namespace Tcp
    {
        class Program
        {
            static void Main(string[] args)
            {
                using (var context = NetMQContext.Create())
                {
                    using (var server = context.CreateResponseSocket())
                    {
                        server.Bind("tcp://*:5555");

                        using (var client = context.CreateRequestSocket())
                        {
                            client.Connect("tcp://localhost:5555");
                            Console.WriteLine("Sending Hello");
                            client.Send("Hello");

                            var message = server.ReceiveString();
                            Console.WriteLine("Received {0}", message);

                            Console.WriteLine("Sending World");
                            server.Send("World");

                            message = client.ReceiveString();
                            Console.WriteLine("Received {0}", message);

                            Console.ReadLine();
                        }

                    }
                }
            }
        }
    }


Which when run gives the following sort of output:

<p>
<i>
Sending Hello<br/>
Received Hello<br/>
Sending World<br/>
Received World<br/>
</i>
</p>


Where it can be seen that the format of the tcp information used in the Bind() and Connect() methods are of the form shown below

tcp://*:5555

This is made up of 3 parts : 

+ [0] the "tcp" protocol part
+ [1] the "*" part, which is either a IP address, or a wild card to match any
+ [2] the "5555" part, which is the port number




InProc
=====

InProc (In process) allows you to connect different parts of your single application. This is actually quite useful, and
you may do this for several reasons:
 
+ To do away with shared state/locks. When you send data down the wire (socket) there is no shared state to worry about
  each end of the socket will have its own copy.
+ Being able to communicate between very disparate parts of a system

NetMQ comes with several components that use InProc, such as [Actor model] (https://github.com/zeromq/netmq/blob/master/docs/actor.md) and [Devices] (https://github.com/zeromq/netmq/blob/master/docs/devices.md), which are discussed
in thier relevant documentation pages.


For now to demonstrate a simple InProc arrangement, lets try and send some message (a string to keep it simple, but this could be some serialized object) between 2 threads.

Here is some demo code:

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetMQ;

    namespace Client
    {
        class Program
        {
            public async Task Start()
            {
                using (var context = NetMQContext.Create())
                {

                    var end1 = context.CreatePairSocket();
                    var end2 = context.CreatePairSocket();
                    end1.Bind("inproc://InprocTest_5555");
                    end2.Connect("inproc://InprocTest_5555");
               

                    var end1Task = Task.Run(() =>
                    {
                        Console.WriteLine("ManagedThreadId = {0}", Thread.CurrentThread.ManagedThreadId);
                        Console.WriteLine("Sending hello down the inproc pipeline");
                        end1.Send("Hello");
                    
                    });


                    var end2Task = Task.Run(() =>
                    {
                        Console.WriteLine("ManagedThreadId = {0}", Thread.CurrentThread.ManagedThreadId);
                        var message = end2.ReceiveString();
                        Console.WriteLine(message);
                    
                    });

                    Task.WaitAll(new[] {end1Task, end2Task}); 

                    end1.Dispose();
                    end2.Dispose();
                }
            }


            static void Main(string[] args)
            {
                Program p = new Program();
                p.Start().Wait();
                Console.ReadLine();
            }
        }
    }


Which when run gives the following sort of output:

<p>
<i>
ManagedThreadId = 12<br/>
ManagedThreadId = 6<br/>
Sending hello down the inproc pipeline<br/>
Hello<br/>
</i>
</p>




Where it can be seen that the format of the Inproc information used in the Bind() and Connect() methods are of the form shown below

**inproc://InprocTest_5555**

This is made up of 2 parts : 

+ [0] the "inproc" protocol part
+ [1] the "InprocTest_5555" part, which is an unique string that suits your needs







PGM
====


Pragmatic General Multicast (PGM) is a reliable multicast transport protocol for applications that require ordered
or unordered, duplicate-free, multicast data delivery from multiple sources to multiple receivers.  

Pgm guarantees that a receiver in the group either receives all data packets from transmissions and repairs, or
is able to detect unrecoverable data packet loss. PGM is specifically intended as a workable solution for multicast
applications with basic reliability requirements. Its central design goal is simplicity of operation with due 
regard for scalability and network efficiency.

To use PGM with NetMQ, we do not have to do too much. We just need to follow these 3 pointers:

1. The socket types are now PublisherSocket and SubscriberSocket
   which are talked about in more detail in the [pub-sub] (https://github.com/zeromq/netmq/blob/master/docs/pub-sub.md) documentation.
2. Make sure you are running the app as "Administrator"
3. Make sure you have turned on the "Multicasting Support". You can do that as follows:


<br/>
![alt text](https://github.com/zeromq/netmq/blob/master/docs/Images/PgmSettingsInWindows.png "PgmSettingsInWindows")



Here is a small demo that use PGM, as well as PublisherSocket and SubscriberSocket and a few option values.

    using System;
    using NetMQ;

    namespace Pgm
    {
        class Program
        {
            static void Main(string[] args)
            {
                const int MegaBit = 1024;
                const int MegaByte = 1024;

                using (NetMQContext context = NetMQContext.Create())
                {
                    using (var pub = context.CreatePublisherSocket())
                    {
                        pub.Options.MulticastHops = 2;
                        pub.Options.MulticastRate = 40 * MegaBit; // 40 megabit
                        pub.Options.MulticastRecoveryInterval = TimeSpan.FromMinutes(10);
                        pub.Options.SendBuffer = MegaByte * 10; // 10 megabyte

                        pub.Connect("pgm://224.0.0.1:5555");

                        using (var sub = context.CreateSubscriberSocket())
                        {
                            using (var sub2 = context.CreateSubscriberSocket())
                            {
                                sub.Options.ReceivevBuffer = MegaByte * 10;
                                sub2.Options.ReceivevBuffer = MegaByte * 10;

                                sub.Bind("pgm://224.0.0.1:5555");
                                sub2.Bind("pgm://224.0.0.1:5555");

                                sub.Subscribe("");
                                sub2.Subscribe("");

                                Console.WriteLine("Server sending 'Hi'");
                                pub.Send("Hi");

                                bool more;
                                string message = sub.ReceiveString(out more);
                                Console.WriteLine("sub message = '{0}'", message);

                                message = sub2.ReceiveString(out more);
                                Console.WriteLine("sub2 message = '{0}'", message);


                                Console.ReadLine();
                            }
                        }
                    }
                }
            }
        }
    }


Which when run gives the following sort of output:

<p>
<i>
Server sending 'Hi'<br/>
sub message = 'Hi'<br/>
sub2 message = 'Hi'<br/>
</i>
</p>



Where it can be seen that the format of the Pgm information used in the Bind() and Connect() methods are of the form shown below

pgm://224.0.0.1:5555

This is made up of 3 parts : 

+ [0] the "pgm" protocol part
+ [1] the "224.0.0.1" part, which is either a TCPIP address, or a wild card to match any
+ [2] the "5555" part, which is the port number


Another good source for Pgm information is actually the [PgmTests] (https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/PgmTests.cs)



