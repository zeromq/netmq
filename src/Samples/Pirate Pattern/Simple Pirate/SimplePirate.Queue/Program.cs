using System;
using System.Collections.Generic;
using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace SimplePirate.Queue
{
    class Program
    {
        private const string LRU_READY = "READY";
        private const string FRONTEND_ENDPOINT = "tcp://localhost:5555";
        private const string BACKEND_ENDPOINT = "tcp://localhost:5556";

        private static Queue<byte[]> workerQueue;
        private static INetMQSocket frontend;
        private static INetMQSocket backend;

        static void Main(string[] args)
        {
            using (var context = new Factory().CreateContext())
            {
                using (frontend = context.CreateRouterSocket())
                {
                    using (backend = context.CreateRouterSocket())
                    {

                        // For Clients
                        Console.WriteLine("Q: Binding frontend {0}", FRONTEND_ENDPOINT);
                        frontend.Bind(FRONTEND_ENDPOINT);

                        // For Workers
                        Console.WriteLine("Q: Binding backend {0}", BACKEND_ENDPOINT);
                        backend.Bind(BACKEND_ENDPOINT);

                        //  Logic of LRU loop
                        //  - Poll backend always, frontend only if 1+ worker ready
                        //  - If worker replies, queue worker as ready and forward reply
                        //    to client if necessary
                        //  - If client requests, pop next worker and send request to it

                        //  Queue of available workers
                        workerQueue = new Queue<byte[]>();

                        //  Handle worker activity on backend
                        backend.ReceiveReady += BackendOnReceiveReady;
                        frontend.ReceiveReady += FrontendOnReceiveReady;

                        while (true)
                        {
                            backend.Poll(TimeSpan.FromMilliseconds(500));
                            if (workerQueue.Count > 0)
                                frontend.Poll(TimeSpan.FromMilliseconds(500));
                        }
                    }
                }
            }
        }

        private static void FrontendOnReceiveReady(object sender, NetMQSocketEventArgs socket)
        {
            //  Now get next client request, route to next worker
            //  Dequeue and drop the next worker address

            //  Now get next client request, route to LRU worker
            //  Client request is [address][empty][request]
            byte[] clientAddr = socket.Socket.Receive();
            byte[] empty = socket.Socket.Receive();
            byte[] request = socket.Socket.Receive();

            byte[] deq;
            try
            {
                deq = workerQueue.Dequeue();
                backend.SendMore(deq);
                backend.SendMore(Encoding.Unicode.GetBytes(""));
                backend.SendMore(clientAddr);
                backend.SendMore(Encoding.Unicode.GetBytes(""));
                backend.Send(request);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Q: [FrontendOnReceiveReady] Dequeue exc: {0}", exc.ToString());
            }
        }

        private static void BackendOnReceiveReady(object sender, NetMQSocketEventArgs socket)
        {
            //  Queue worker address for LRU routing
            byte[] workerAddress = socket.Socket.Receive();

            //  Use worker address for LRU routing
            workerQueue.Enqueue(workerAddress);

            //  Second frame is empty
            byte[] empty = socket.Socket.Receive();

            //  Third frame is READY or else a client reply address
            byte[] clientAddress = socket.Socket.Receive();

            //  If client reply, send rest back to frontend
            //  Forward message to client if it's not a READY
            if (Encoding.Unicode.GetString(clientAddress) != LRU_READY)
            {
                empty = socket.Socket.Receive();
                byte[] reply = socket.Socket.Receive();

                frontend.SendMore(clientAddress);
                frontend.SendMore("");
                frontend.Send(reply);
            }
        }
    }
}
