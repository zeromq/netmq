using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.Sleep(2000);

            Ctx ctx = ZMQ.zmq_ctx_new();

            SocketBase req = ctx.create_socket(ZMQ.ZMQ_REQ);

            //SocketBase rep = ctx.create_socket(ZMQ.ZMQ_REP);

            //rep.bind("tcp://127.0.0.1:8000");

            req.connect("tcp://127.0.0.1:8000");

            string message = "HelloHelloHelloHelloHelloHelloHelloHelloHelloHello";

            byte[] data = Encoding.ASCII.GetBytes(message);

            var msg = ZMQ.zmq_msg_init_size(data.Length);

            Buffer.BlockCopy(data, 0, msg.get_data(), 0, data.Length);

            ZMQ.zmq_sendmsg(req, msg, 0);

            var replyMsg = ZMQ.s_recvmsg(req, 0);

            //var reqMsg = ZMQ.zmq_recvmsg(rep, 0);

            Console.WriteLine("Message Received");

            Console.ReadLine();

        
        
        }
    }
}
