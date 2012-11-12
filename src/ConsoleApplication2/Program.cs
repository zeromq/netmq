using System;
using System.Collections.Generic;
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
			Context context = Context.Create();

			PairSocket pairSocket1 = context.CreatePairSocket();
			PairSocket pairSocket2 = context.CreatePairSocket();

			//RequestSocket pairSocket1 = context.CreateRequestSocket();
			//ResponseSocket pairSocket2 = context.CreateResponseSocket();


			pairSocket1.Bind("inproc://d");
			pairSocket2.Connect("inproc://d");

			pairSocket1.Send("1");

			bool ok = pairSocket2.Poll(TimeSpan.FromSeconds(2), NetMQ.zmq.PollEvents.PollIn);
			
			//pairSocket1.Send("1");

			bool hasMore;
			
			string m = pairSocket2.ReceiveString(out hasMore);

			//var j = pairSocket2.ReceiveString(true, out hasMore);

			//pairSocket1.Send("1");



			
		
			Console.WriteLine(m);
		}
	}
}
