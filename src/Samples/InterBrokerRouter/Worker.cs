using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace InterBrokerRouter
{
    public class Worker
    {
        private readonly string m_localBackendAddress;
        private readonly byte m_id;

        /// <summary>
        /// The worker connects its <see cref="RequestSocket"/> to the broker's backend <see cref="RouterSocket"/>.
        /// Upon startup, the worker sends a READY message to the broker.
        /// The worker receives a message from the broker and simply returns it after printing it on screen.
        /// It simulates work by sleeping an arbitrary period of up to two seconds.
        /// </summary>
        /// <param name="localBackEndAddress">The broker's backend address to connect to.</param>
        /// <param name="id">The id of the worker.</param>
        public Worker(string localBackEndAddress, byte id)
        {
            m_localBackendAddress = localBackEndAddress;
            m_id = id;
        }

        public void Run()
        {
            var rnd = new Random(m_id);

            using (var worker = new RequestSocket())
            {
                worker.Connect(m_localBackendAddress);

                Console.WriteLine("[WORKER {0}] Connected & READY", m_id);

                // build READY message
                var msg = new NetMQMessage();
                var ready = NetMQFrame.Copy(new[] { Program.WorkerReady });

                msg.Append(ready);
                msg.Push(NetMQFrame.Empty);
                msg.Push(new[] { m_id });

                // and send to broker
                worker.SendMultipartMessage(msg);

                while (true)
                {
                    // wait for a request - the REQ might be from a local client or a cloud request
                    var request = worker.ReceiveMultipartMessage();

                    if (request.FrameCount < 3)
                    {
                        Console.WriteLine("[WORKER {0}] ERR - received an empty message", m_id);
                        break; // something went wrong -> exit
                    }

                    Console.WriteLine("[WORKER {0}] received", m_id);

                    foreach (var frame in request)
                        Console.WriteLine("\t[{0}", frame.ConvertToString());

                    // simulate working for an arbitrary time < 2s
                    Thread.Sleep(rnd.Next(2000));
                    // simply send back what we received
                    worker.SendMultipartMessage(request);
                }
            }
        }
    }
}