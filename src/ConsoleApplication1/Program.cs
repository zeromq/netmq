using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;

namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{		
			Context context = Context.Create();

			RequestSocket req = context.CreateRequestSocket();

			ResponseSocket rep = context.CreateResponseSocket();			

			rep.Bind("tcp://127.0.0.1:8000");			

			req.Connect("tcp://127.0.0.1:8000");

			Thread.Sleep(1000);

			string m1 = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHello";

			req.Send(m1);

			bool hasMore;

			var m2 = rep.ReceiveString(out hasMore);

			Console.WriteLine(m2);

			var m3 = "hello back";

			rep.Send(m3);

			var m4 = req.ReceiveString(out hasMore);

			Console.WriteLine(m4);			

			Console.WriteLine("Message Received");

			Console.ReadLine();
		}
	}
}
