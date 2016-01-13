using System;
using System.Collections.Generic;
using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace SimplePirate.Queue
{
    internal static class Program
    {
        private const string LRUReady = "READY";
        private const string FrontendEndpoint = "tcp://localhost:5555";
        private const string BackendEndpoint = "tcp://localhost:5556";

        private static void Main()
        {
            using (var frontend = new RouterSocket())
            using (var backend = new RouterSocket())
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
                backend.ReceiveReady += (s, e) =>
                {
                    // Queue worker address for LRU routing
                    byte[] workerAddress = e.Socket.ReceiveFrameBytes();

                    // Use worker address for LRU routing
                    workerQueue.Enqueue(workerAddress);

                    // Second frame is empty
                    e.Socket.SkipFrame();

                    // Third frame is READY or else a client reply address
                    byte[] clientAddress = e.Socket.ReceiveFrameBytes();

                    // If client reply, send rest back to frontend
                    // Forward message to client if it's not a READY
                    if (Encoding.Unicode.GetString(clientAddress) != LRUReady)
                    {
                        e.Socket.SkipFrame(); // empty

                        byte[] reply = e.Socket.ReceiveFrameBytes();

                        frontend.SendMoreFrame(clientAddress);
                        frontend.SendMoreFrame("");
                        frontend.SendFrame(reply);
                    }
                };

                frontend.ReceiveReady += (s, e) =>
                {
                    // Now get next client request, route to next worker
                    // Dequeue and drop the next worker address

                    // Now get next client request, route to LRU worker
                    // Client request is [address][empty][request]
                    byte[] clientAddr = e.Socket.ReceiveFrameBytes();
                    e.Socket.SkipFrame(); // empty
                    byte[] request = e.Socket.ReceiveFrameBytes();

                    try
                    {
                        byte[] deq = workerQueue.Dequeue();
                        backend.SendMoreFrame(deq);
                        backend.SendMoreFrame(Encoding.Unicode.GetBytes(""));
                        backend.SendMoreFrame(clientAddr);
                        backend.SendMoreFrame(Encoding.Unicode.GetBytes(""));
                        backend.SendFrame(request);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Q: [FrontendOnReceiveReady] Dequeue exception: {0}", ex.ToString());
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