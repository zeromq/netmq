using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;
using zmq;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            Context context = Context.Create();

            PublisherSocket pub = context.CreatePublisherSocket();

            SubscriberSocket sub = context.CreateSubscriberSocket();           

            pub.Bind("tcp://127.0.0.1:8000");

            sub.Connect("tcp://127.0.0.1:8000");

            sub.Subscribe("hello");

            Thread.Sleep(500);

            //while (true)
            //{

            //            
            pub.SendTopic("hello").SendMore("message").Send("hahhhh");

            //                Console.WriteLine("Done");
            //}

            var messages = sub.ReceiveAllString();

            foreach (var m in messages)
            {
                Console.WriteLine(m);
            }

            Console.ReadLine();

        }
    }
}
