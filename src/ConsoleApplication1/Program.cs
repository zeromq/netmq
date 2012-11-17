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

			SubscriberSocket sub = context.CreateSubscriberSocket();
			sub.Subscribe("");


			PublisherSocket pub = context.CreatePublisherSocket();

			pub.Connect("pgm://224.0.0.1:55555");

			Console.ReadLine();
			
			sub.Bind("pgm://224.0.0.1:55555");			

			Thread.Sleep(200);
			
			pub.SendTopic("Shalom").Send("BLABLABLABLA");
			pub.SendTopic("Shalom").Send("BLABLABLABLA");

			Thread.Sleep(200);
						
			pub.SendTopic("Shalom").Send("BLABLABLABLA");

			IList<string> messages = sub.ReceiveAllString();

			Console.WriteLine(messages[0]);

		}
	}
}
