using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
	class Program
	{
		static void Main(string[] args)
		{
			var p = new SubjectNetMQ<int>(zeroMqAddress: "tcp://localhost:56000");
			p.Subscribe(o =>
			{
				Console.Write($"Hello, world! {o}\n");
			});

			p.OnNext(4);

			Console.Write("[any key]\n");
			Console.ReadKey();

		}
	}
}
