using System;
using System.IO;
using System.Text;
using System.Threading;

namespace NetMQ.SimpleTests
{
    internal class HelloWorld : ITest
    {
        public string TestName
        {
            get { return "Hello World"; }
        }

        public void RunTest()
        {
            var client = new Thread(ClientThread);
            var server = new Thread(ServerThread);

            server.Start();
            client.Start();

            server.Join();
            client.Join();
        }

        private static void ClientThread()
        {
            Thread.Sleep(10);

            using (var context = NetMQContext.Create())
            using (var socket = context.CreateRequestSocket())
            {
                socket.Connect("tcp://127.0.0.1:8989");

                socket.Send(new NetMQFrame(Encoding.UTF8.GetBytes("Hello")).Buffer);

                byte[] buffer = socket.Receive();

                using (var stream = new MemoryStream(buffer, 0, buffer.Length))
                {
                    Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
                }
            }
        }

        private static void ServerThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreateResponseSocket())
            {
                socket.Bind("tcp://*:8989");

                var request = new NetMQFrame(socket.Receive());
                Console.WriteLine(Encoding.UTF8.GetString(request.Buffer));

                socket.Send(new NetMQFrame(Encoding.UTF8.GetBytes("World")).Buffer);
            }
        }
    }
}