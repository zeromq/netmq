using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            Ctx ctx = ZMQ.CtxNew();


            SocketBase rep = ctx.CreateSocket(ZMQ.ZMQ_REP);

            rep.Bind("tcp://127.0.0.1:8000");
           
            //string message = "Hello";

            //byte[] data = Encoding.ASCII.GetBytes(message);

            //var msg = ZMQ.zmq_msg_init_size(data.Length);

            //Buffer.BlockCopy(data, 0, msg.get_data(), 0, data.Length);

            //ZMQ.zmq_sendmsg(req, msg, 0);

            var reqMsg = ZMQ.recvmsg(rep, 0);

            var reply = ZMQ.ZmqMsgInitSize(5);
            reply.Data()[0] = 1;
            reply.Data()[1] = 2;
            reply.Data()[2] = 3;
            reply.Data()[3] = 4;
            reply.Data()[4] = 5;

            Console.WriteLine("Recieved Message");

            ZMQ.Send(rep, reply, 0);

            Console.ReadLine();

        }
    }
}
