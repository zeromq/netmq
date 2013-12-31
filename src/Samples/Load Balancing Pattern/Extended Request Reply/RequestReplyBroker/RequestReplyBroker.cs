using NetMQ;

namespace ExtendedRequestReply
{
    internal class RequestReplyBroker
    {
        private const string FRONTEND_ENDPOINT = "tcp://127.0.0.1:5559";
        private const string BACKEND_ENDPOINT = "tcp://127.0.0.1:5560";

        private static void Main(string[] args)
        {
            using (var ctx = NetMQContext.Create())
            {
                using (NetMQSocket frontend = ctx.CreateRouterSocket(), backend = ctx.CreateDealerSocket())
                {
                    frontend.Bind(FRONTEND_ENDPOINT);
                    backend.Bind(BACKEND_ENDPOINT);

                    //Handler for messages coming in to the frontend
                    frontend.ReceiveReady += (sender, e) =>
                        {
                            var msg = frontend.ReceiveMessage();
                            backend.SendMessage(msg); //Relay this message to the backend
                        };

                    //Handler for messages coming in to the backend
                    backend.ReceiveReady += (sender, e) =>
                        {
                            var msg = backend.ReceiveMessage();
                            frontend.SendMessage(msg); //Relay this message to the frontend
                        };

                    using (var poller = new Poller())
                    {
                        poller.AddSocket(backend);
                        poller.AddSocket(frontend);
                    
                        //Listen out for events on both sockets and raise events when messages come in
                        poller.Start();
                    }
                }
            }
        }
    }
}
