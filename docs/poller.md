Handling Multiple Sockets
=====

So why would you want to handle multiple sockets anyway? Well, there are a variety of reasons, such as:

+ You may have multiple sockets within one process that rely on each other, and the timings are such that you need to know that the socket(s) are ready before it/they can receive anything
+ You may have a Request, as well as a Publisher socket in one process

There are times you may end up with more than one socket per process. And there may be occasions when you only want to use the socket(s) when they are deemed ready.

ZeroMQ actually has a concept of a <code>Poller</code> that can be used to determine if a socket is deemed ready to use.

NetMQ has an implementation of the <code>Poller</code>, and it can be used to do the following things:

+ Monitor a single socket, for readiness
+ Monitor an <code>IEnumerable<NetMQSocket></code> for readiness
+ Allow <code>NetMQSocket</code>(s) to be added dynamically and still report on the readiness of the new sockets
+ Allow <code>NetMQSocket</code>(s) to be removed dynamically
+ Raise an event on the socket instance when it is ready


## Poller Methods

There are several methods available on the <code>Poller</code> to help you. Most notably <code>AddSocket(..)/RemoveSocket(..)</code> and <code>Start()/Stop()</code>. 

The idea is that you would use the <code>AddSocket</code> to add the socket you want to monitor for "readiness" to the <code>Poller</code> instance, and then some time later call the <code>Poller.Start()</code> method, at which point the <code>Poller</code> will call back any registered <code>ReceiveReady</code> event handler delegates


## Poller Example

So now that you know what the "Poller" does, perhaps it is time to see an example. 

The code below is a fully working Console application that demonstrates a single socket being added to the <code>Poller</code>. It can also be seen that the <code>ReceiveReady</code> event is hooked up too. The <code>Poller</code> will call this event handler back when the Socket (the one that is added to the <code>Poller</code>) is "Ready".


    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using NetMQ;

    namespace ConsoleApplication1
    {
        class Program
        {
            static void Main(string[] args)
            {
                using (NetMQContext contex = NetMQContext.Create())
                {
                    using (var rep = contex.CreateResponseSocket())
                    {
                        rep.Bind("tcp://127.0.0.1:5002");

                        using (var req = contex.CreateRequestSocket())
                        using (Poller poller = new Poller())
                        {
                            req.Connect("tcp://127.0.0.1:5002");

                            //The ReceiveReady event is raised by the Poller
                            rep.ReceiveReady += (s, a) =>
                            {
                                bool more;
                                string messageIn = a.Socket.ReceiveString(out more);
                                Console.WriteLine("messageIn = {0}", messageIn);
                                a.Socket.Send("World");
                            };

                            poller.AddSocket(rep);

                            Task pollerTask = Task.Factory.StartNew(poller.Start);
                            req.Send("Hello");

                            bool more2;
                            string messageBack = req.ReceiveString(out more2);
                            Console.WriteLine("messageBack = {0}", messageBack);

                            poller.Stop();

                            Thread.Sleep(100);
                            pollerTask.Wait();
                        }
                    }
                }
                Console.ReadLine();
            }
        }
    }


When you run this you should see something like this appear in the Console output:

<p>
<i>
messageIn = Hello<br/>  
messageBack = World<br/>
</i>
</p>




Building on this example. What we can now do is to remove the <code>ResponseSocket</code> from the <code>Poller</code> once we see the 1st message, which should mean that we no longer recieve any messages on the removed <code>ResponseSocket</code>. We will stick with the same example code, but this time we have added a <code>Poller.RemoveSocket(..)</code> in the <code>rep.ReceiveReady</code> event handler code.

Here is the new modified code


    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using NetMQ;

    namespace ConsoleApplication1
    {
        class Program
        {
            static void Main(string[] args)
            {

                using (NetMQContext contex = NetMQContext.Create())
                {
                    using (var rep = contex.CreateResponseSocket())
                    {
                        rep.Bind("tcp://127.0.0.1:5002");

                        using (var req = contex.CreateRequestSocket())
                        using (Poller poller = new Poller())
                        {
                            req.Connect("tcp://127.0.0.1:5002");

                            //The ReceiveReady event is raised by the Poller
                            rep.ReceiveReady += (s, a) =>
                            {
                                bool more;
                                string messageIn = a.Socket.ReceiveString(out more);
                                Console.WriteLine("messageIn = {0}", messageIn);
                                a.Socket.Send("World");


                                //REMOVAL
                                //This time we remove the Socket from the Poller, so it should not receive any more messages
                                poller.RemoveSocket(a.Socket);
                            };

                            poller.AddSocket(rep);



                            Task pollerTask = Task.Factory.StartNew(poller.Start);
                            
                            req.Send("Hello");

                            bool more2;
                            string messageBack = req.ReceiveString(out more2);
                            Console.WriteLine("messageBack = {0}", messageBack);


        
                            //This should not do anything, as we removed the ResponseSocket
                            //the 1st time we sent a message to it
                            req.Send("Hello Again");

                           
                            Console.WriteLine("Carrying on doing the rest");

                            poller.Stop();

                            Thread.Sleep(100);
                            pollerTask.Wait();
                        }
                    }
                }
                Console.ReadLine();
            }
        }
    }


Which when run gives this output now.

<p><i>
messageIn = Hello<br/>  
messageBack = World<br/>
Carrying on doing the rest<br/>
</i>
</p>


See how we did not get any output for the "Hello Again" message we attempted to send. This is due to the <code>ResponseSocket</code> being removed from the <code>Poller</code> earlier.


## Timer(s)

Another thing the Poller allows is to add/remove <code>NetMQTimer</code> instances, which you may do using the <code>AddTimer(..) / RemoveTimer(..)</code> methods. 

Where the added timers get called back the <code>Poller</code>. Here is a simple example that adds a <code>NetMQTimer</code> which expects to wait for 5 Seconds. The <code>NetMQTimer</code> instance is added to the <code>Poller</code>, which internally calls the <code>NetMQTimer.Elapsed</code> event handler callback delegates.

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using NetMQ;

    namespace ConsoleApplication1
    {
        class Program
        {
            static void Main(string[] args)
            {

                using (Poller poller = new Poller())
                {
                    NetMQTimer timer = new NetMQTimer(TimeSpan.FromSeconds(5));
                    timer.Elapsed += (s, a) =>
                            {
                                Console.WriteLine("Timer done");
                            }; ;
                    poller.AddTimer(timer);


                    Task pollerTask = Task.Factory.StartNew(poller.Start);


                    //give the poller enough time to run the timer (set at 5 seconds)
                    Thread.Sleep(10000);

                }

                Console.ReadLine();
            }
        }
    }


Which when run gives this output now.

<p><i>
Timer done<br/>  
</i>
</p>




## Further Reading

Another good place to look is at the test cases [Poller tests]( https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/PollerTests.cs)
