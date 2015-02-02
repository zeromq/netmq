using System;
using System.Threading;

using NetMQ;

namespace InterBrokerRouter
{
    public class Worker
    {
        private readonly NetMQContext _ctx;
        private readonly string _localBackendAddress;

        public Worker (NetMQContext context, string localBackEndAddress)
        {
            _ctx = context;
            _localBackendAddress = localBackEndAddress;
        }

        public void Run ()
        {
            var rnd = new Random ();
            var id = rnd.Next (1000);

            using (var worker = _ctx.CreateRequestSocket ())
            {
                worker.Connect (_localBackendAddress);

                Console.WriteLine ("[WORKER {0}]: Connected & READY", id);

                // send READY to broker
                worker.Send (new byte[] { Program.WORKER_READY });

                while (true)
                {
                    // wait for a request
                    var request = worker.Receive ();

                    if (request.Length == 0)
                    {
                        Console.WriteLine ("[WORKER {0}]: ERR - received an empty message", id);
                        break;      // something went wrong -> exit
                    }

                    Console.WriteLine ("[WORKER {0}]: received [{1}][{2}][{3}]", id, request[0], request[1], request[2]);
                    // simulate working for an arbitrary time < 2s
                    Thread.Sleep (rnd.Next (2000));
                    // simply send back what we received
                    worker.Send (request);
                }
            }
        }
    }
}
