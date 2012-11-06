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

            Thread.Sleep(2000);

            //while (true)
            //{

//            
                pub.SendTopic("hello").Send("message");

//                Console.WriteLine("Done");
            //}

            bool isMore ;

            string message = sub.ReceiveString(out isMore);

            Console.WriteLine(message);

            message = sub.ReceiveString(out isMore);

            Console.WriteLine(message);


            Console.ReadLine();

        }
    }
}
