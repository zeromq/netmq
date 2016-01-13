using NetMQ;
using NetMQ.Sockets;

namespace ExtendedRequestReply
{
    internal static class RequestReplyBroker
    {
        private static void Main()
        {
            using (var frontend = new RouterSocket("@tcp://127.0.0.1:5559"))
            using (var backend = new DealerSocket("@tcp://127.0.0.1:5560"))
            {
                // Handler for messages coming in to the frontend
                frontend.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();
                    backend.SendMultipartMessage(msg); // Relay this message to the backend
                };

                // Handler for messages coming in to the backend
                backend.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();
                    frontend.SendMultipartMessage(msg); // Relay this message to the frontend
                };

                using (var poller = new NetMQPoller {backend, frontend})
                {
                    // Listen out for events on both sockets and raise events when messages come in
                    poller.Run();
                }
            }
        }
    }
}