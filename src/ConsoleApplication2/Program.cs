using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            Ctx ctx = ZMQ.zmq_ctx_new();


            SocketBase rep = ctx.create_socket(ZMQ.ZMQ_REP);

            rep.bind("tcp://127.0.0.1:8000");
           
            //string message = "Hello";

            //byte[] data = Encoding.ASCII.GetBytes(message);

            //var msg = ZMQ.zmq_msg_init_size(data.Length);

            //Buffer.BlockCopy(data, 0, msg.get_data(), 0, data.Length);

            //ZMQ.zmq_sendmsg(req, msg, 0);

            var reqMsg = ZMQ.zmq_recvmsg(rep, 0);

            var reply = ZMQ.zmq_msg_init_size(5);
            reply.get_data()[0] = 1;
            reply.get_data()[1] = 2;
            reply.get_data()[2] = 3;
            reply.get_data()[3] = 4;
            reply.get_data()[4] = 5;

            Console.WriteLine("Recieved Message");

            ZMQ.zmq_send(rep, reply, 0);

            Console.ReadLine();

        }
    }
}
