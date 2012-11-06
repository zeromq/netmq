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

					ResponseSocket rep = context.CreateResponseSocket();

					rep.Bind("tcp://127.0.0.1:8000");

					while (true)
					{						
						bool hasMore;

						var m2 = rep.ReceiveString(out hasMore);

						Console.WriteLine(m2);

						var m3 = "hello back";

						rep.Send(m3);

						Console.WriteLine("Done");
					}

        	Console.ReadLine();

        }
    }
}
