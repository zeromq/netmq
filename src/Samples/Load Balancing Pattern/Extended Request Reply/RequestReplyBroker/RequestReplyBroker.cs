using NetMQ;

namespace ExtendedRequestReply
{
    internal static class RequestReplyBroker
    {
        private const string FrontendEndpoint = "tcp://127.0.0.1:5559";
        private const string BackendEndpoint = "tcp://127.0.0.1:5560";

        private static void Main()
        {
            using (var context = NetMQContext.Create())
            using (var frontend = context.CreateRouterSocket())
            using (var backend = context.CreateDealerSocket())
            {
                frontend.Bind(FrontendEndpoint);
                backend.Bind(BackendEndpoint);

                // Handler for messages coming in to the frontend
                frontend.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();
                    backend.SendMessage(msg); // Relay this message to the backend
                };

                // Handler for messages coming in to the backend
                backend.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();
                    frontend.SendMessage(msg); // Relay this message to the frontend
                };

                using (var poller = new Poller())
                {
                    poller.AddSocket(backend);
                    poller.AddSocket(frontend);

                    // Listen out for events on both sockets and raise events when messages come in
                    poller.PollTillCancelled();
                }
            }
        }
    }
}