using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;

namespace ConsoleApplication2
{
	class Program
	{
		static void Main(string[] args)
		{
			using (NetMQContext context = NetMQContext.Create())
			{

				using (var socket1 = context.CreateResponseSocket())
				{
					socket1.Bind("tcp://127.0.0.1:82");

					while (true)
					{
						Stopwatch sw = Stopwatch.StartNew();
						using (var socket2 = context.CreateRequestSocket())
						{
							socket2.Options.DelayAttachOnConnect = false;
							socket2.Connect("tcp://127.0.0.1:82");
							sw.Stop();	
						}
						
						Console.WriteLine(sw.ElapsedMilliseconds);
						//Thread.Sleep(1000);
					}
				}
			}
		}
	}
}
