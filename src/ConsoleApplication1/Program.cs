using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			Context context = Context.Create();

			RequestSocket req = context.CreateRequestSocket();

			Poller poller = null;

			Task.Factory.StartNew(() =>
				{					
					ResponseSocket rep = context.CreateResponseSocket();

					MonitoringEventsHandler monitoringEventHandler = new MonitoringEventsHandler();
					monitoringEventHandler.OnAccepted = (address, h) => Console.WriteLine("Accepted");
					monitoringEventHandler.OnListening = (address, h) => Console.WriteLine("Listening");
										
					poller = new Poller(context);

					poller.AddMonitor(rep, "inproc://rep.inproc", monitoringEventHandler, true);

					rep.Bind("tcp://127.0.0.1:8000");
					poller.AddSocket(rep, s =>
					{
						bool hasMore;

						var m2 = s.ReceiveString(out hasMore);
						Console.WriteLine(m2);

						var m3 = "hello back";

						s.Send(m3);
					});

					
					poller.Start();

				});

			Thread.Sleep(1000);

	//		Console.ReadLine();

			req.Connect("tcp://127.0.0.1:8000");		

			string m1 = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHello";

			req.Send(m1);
			
			bool hasMore2;			
						
			var m4 = req.ReceiveString(out hasMore2);

			Console.WriteLine(m4);

			Console.WriteLine("Message Received");

			Console.ReadLine();

			poller.Stop(true);
		}
	}
}
