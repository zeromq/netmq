using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace NetMQ.ReactiveExtensions.SampleClient
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Write("Reactive Extensions subscriber demo:\n");

			SubjectNetMQ<MyMessage> subject = new SubjectNetMQ<MyMessage>("tcp://127.0.0.1:56001");
			subject.Subscribe(message =>
			{
				Console.Write("Received: {0}, '{1}'.\n", message.Num, message.Name);
			});

			Console.WriteLine("[waiting for publisher - any key to exit]");
			Console.ReadKey();
		}
	}

	[ProtoContract]
	public class MyMessage
	{
		public MyMessage(int num, string name)
		{
			Num = num;
			Name = name;
		}

		[ProtoMember(1)]
		public int Num { get; set; }

		[ProtoMember(2)]
		public string Name { get; set; }
	}
}