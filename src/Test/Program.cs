using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{
	class Program
	{
		static void Main(string[] args)
		{	
			/*{
				var p = new SubjectNetMQ<int>(zeroMqAddress: "tcp://localhost:56000", queueName: "test1");
				p.Subscribe(o =>
				{
					Console.Write($"Hello, world 1! {o}\n");
				});

				p.OnNext(4);
			}*/


			{
				var p = new SubjectNetMQ<int>(zeroMqAddress: "tcp://localhost:56001", queueName: "test2");
				p.OnNext(5);

				p.Subscribe(o =>
				{
					Console.Write($"Hello, world 2! {o}\n");
				});

				Thread.Sleep(TimeSpan.FromSeconds(5));

				for (int i = 0; i < 2000; i++)
				{
					p.OnNext(i);
					Thread.Sleep(TimeSpan.FromMilliseconds(10));
				}
				Console.Write("[any key]\n");
				Console.ReadKey();
			}
		}
	}
}
