using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;
using System.Threading.Tasks;
using NetMQ.Sockets;

namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			Context context = Context.Create();

			SubscriberSocket sub = context.CreateSubscriberSocket();
			sub.Subscribe("");


			PublisherSocket pub = context.CreatePublisherSocket();

			pub.Connect("pgm://192.168.0.103;224.0.0.1:55555");

			Console.ReadLine();

			sub.Bind("pgm://192.168.0.103;224.0.0.1:55555");			

			Thread.Sleep(200);
			
			pub.SendTopic("Shalom").Send("1");
			pub.SendTopic("Shalom").Send("2");

			Thread.Sleep(200);
						
			pub.SendTopic("Shalom").Send("3");

			
			while (true)
			{
				IList<string> messages = sub.ReceiveAllString();

				foreach (string message in messages)
				{
					Console.WriteLine(message);
				}
			}
		}
	}
}
