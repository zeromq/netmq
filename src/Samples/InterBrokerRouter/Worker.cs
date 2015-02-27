using System;
using System.Threading;

using NetMQ;

namespace InterBrokerRouter
{
    public class Worker
    {
        private readonly string m_localBackendAddress;
        private readonly byte m_id;

        /// <summary>
        ///     the worker connects its REQ socket to the broker's backend ROUTER socket
        ///     upon start up the worker sends a READY message to the broker
        ///     the worker receives a message from the broker and simply returns it after
        ///     printing it on screen
        ///     it simulates the work by sleeping an arbitrary time but less than 2s
        /// </summary>
        /// <param name="localBackEndAddress">the broker's backend address to connect to</param>
        /// <param name="id">the id of the worker</param>
        public Worker (string localBackEndAddress, byte id)
        {
            m_localBackendAddress = localBackEndAddress;
            m_id = id;
        }

        public void Run ()
        {
            var rnd = new Random (m_id);

            using (var ctx = NetMQContext.Create ())
            using (var worker = ctx.CreateRequestSocket ())
            {
                worker.Connect (m_localBackendAddress);

                Console.WriteLine ("[WORKER {0}] Connected & READY", m_id);

                // build READY message
                var msg = new NetMQMessage ();
                var ready = NetMQFrame.Copy (new[] { Program.WorkerReady });

                msg.Append (ready);
                msg.Push (NetMQFrame.Empty);
                msg.Push (new[] { m_id });

                // and send to broker
                worker.SendMessage (msg);

                while (true)
                {
                    // wait for a request - the REQ might be from a local client or a cloud request
                    var request = worker.ReceiveMessage ();

                    if (request.FrameCount < 3)
                    {
                        Console.WriteLine ("[WORKER {0}] ERR - received an empty message", m_id);
                        break;      // something went wrong -> exit
                    }

                    Console.WriteLine ("[WORKER {0}] received", m_id);

                    foreach (var frame in request)
                        Console.WriteLine ("\t[{0}", frame.ConvertToString ());

                    // simulate working for an arbitrary time < 2s
                    Thread.Sleep (rnd.Next (2000));
                    // simply send back what we received
                    worker.SendMessage (request);
                }
            }
        }
    }
}
