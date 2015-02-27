using System;
using System.Collections.Generic;
using System.Text;
using NetMQ;

namespace SimplePirate.Queue
{
    internal static class Program
    {
        private const string LRUReady = "READY";
        private const string FrontendEndpoint = "tcp://localhost:5555";
        private const string BackendEndpoint = "tcp://localhost:5556";

        private static void Main()
        {
            using (var context = NetMQContext.Create())
            using (var frontend = context.CreateRouterSocket())
            using (var backend = context.CreateRouterSocket())
            {
                // For Clients
                Console.WriteLine("Q: Binding frontend {0}", FrontendEndpoint);
                frontend.Bind(FrontendEndpoint);

                // For Workers
                Console.WriteLine("Q: Binding backend {0}", BackendEndpoint);
                backend.Bind(BackendEndpoint);

                // Logic of LRU loop
                // - Poll backend always, frontend only if 1+ worker ready
                // - If worker replies, queue worker as ready and forward reply
                //   to client if necessary
                // - If client requests, pop next worker and send request to it

                // Queue of available workers
                var workerQueue = new Queue<byte[]>();

                // Handle worker activity on backend
                backend.ReceiveReady += (sender, socket) =>
                {
                    // Queue worker address for LRU routing
                    byte[] workerAddress = socket.Socket.Receive();

                    // Use worker address for LRU routing
                    workerQueue.Enqueue(workerAddress);

                    // Second frame is empty
                    socket.Socket.Receive();

                    // Third frame is READY or else a client reply address
                    byte[] clientAddress = socket.Socket.Receive();

                    // If client reply, send rest back to frontend
                    // Forward message to client if it's not a READY
                    if (Encoding.Unicode.GetString(clientAddress) != LRUReady)
                    {
                        socket.Socket.Receive(); // empty

                        byte[] reply = socket.Socket.Receive();

                        frontend.SendMore(clientAddress);
                        frontend.SendMore("");
                        frontend.Send(reply);
                    }
                };

                frontend.ReceiveReady += (sender1, socket1) =>
                {
                    // Now get next client request, route to next worker
                    // Dequeue and drop the next worker address

                    // Now get next client request, route to LRU worker
                    // Client request is [address][empty][request]
                    byte[] clientAddr = socket1.Socket.Receive();
                    socket1.Socket.Receive(); // empty
                    byte[] request = socket1.Socket.Receive();

                    try
                    {
                        byte[] deq = workerQueue.Dequeue();
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
                };

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