using System;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace InterBrokerRouter
{
    public class Client
    {
        private readonly string m_localFrontendAddress;
        private readonly string m_monitorAddress;
        private readonly byte m_id;

        /// <summary>
        /// This client will connect its <see cref="RequestSocket"/> to the frontend <see cref="RouterSocket"/> of the broker and
        /// its <see cref="PushSocket"/> to the broker's <see cref="PullSocket"/> as monitor.
        /// It will send a messages and wait at most 10 seconds for an answer before sending an error message via monitor.
        /// If an answer is received it will send a success message via monitor.
        /// </summary>
        /// <param name="localFrontendAddress">The local frontend address of the broker.</param>
        /// <param name="monitorAddress">The monitor address of the broker.</param>
        /// <param name="id">The identity of the client.</param>
        public Client(string localFrontendAddress, string monitorAddress, byte id)
        {
            m_monitorAddress = monitorAddress;
            m_localFrontendAddress = localFrontendAddress;
            m_id = id;
        }

        public void Run()
        {
            Console.WriteLine("[CLIENT {0}] Starting", m_id);

            var rnd = new Random(m_id);
            // each request shall have an unique id in order to recognize an reply for a request
            var messageId = new byte[5];
            // create clientId for messages
            var clientId = new[] { m_id };
            // a flag to signal that an answer has arrived
            bool messageAnswered = false;
            // we use a poller because we have a socket and a timer to monitor
            using (var clientPoller = new NetMQPoller())
            using (var client = new RequestSocket())
            using (var monitor = new PushSocket())
            {
                client.Connect(m_localFrontendAddress);
                monitor.Connect(m_monitorAddress);

                client.Options.Identity = new[] { m_id };
                var timer = new NetMQTimer((int)TimeSpan.FromSeconds(10).TotalMilliseconds);

                // use as flag to indicate exit
                var exit = false;

                // every 10 s check if message has been received, if not then send error message and ext
                // and restart timer otherwise
                timer.Elapsed += (s, e) =>
                {
                    if (messageAnswered)
                    {
                        e.Timer.Enable = true;
                    }
                    else
                    {
                        var msg = string.Format("[CLIENT {0}] ERR - EXIT - lost message {1}", m_id, messageId);
                        // send an error message
                        monitor.SendFrame(msg);

                        // if poller is started than stop it
                        if (clientPoller.IsRunning)
                            clientPoller.Stop();
                        // mark the required exit
                        exit = true;
                    }
                };

                // process arriving answers
                client.ReceiveReady += (s, e) =>
                {
                    // mark the arrival of an answer
                    messageAnswered = true;
                    // worker is supposed to answer with our request id
                    var reply = e.Socket.ReceiveMultipartMessage();

                    if (reply.FrameCount == 0)
                    {
                        // something went wrong
                        monitor.SendFrame(string.Format("[CLIENT {0}] Received an empty message!", m_id));
                        // mark the exit flag to ensure the exit
                        exit = true;
                    }
                    else
                    {
                        var sb = new StringBuilder();

                        // create success message
                        foreach (var frame in reply)
                            sb.Append("[" + frame.ConvertToString() + "]");

                        // send the success message
                        monitor.SendFrame(string.Format("[CLIENT {0}] Received answer {1}",
                            m_id,
                            sb.ToString()));
                    }
                };

                // add socket & timer to poller
                clientPoller.Add(client);
                clientPoller.Add(timer);

                // start poller in another thread to allow the continued processing
                clientPoller.RunAsync();

                // if true the message has been answered
                // the 0th message is always answered
                messageAnswered = true;

                while (!exit)
                {
                    // simulate sporadic activity by randomly delaying
                    Thread.Sleep((int)TimeSpan.FromSeconds(rnd.Next(5)).TotalMilliseconds);

                    // only send next message if the previous one has been replied to
                    if (messageAnswered)
                    {
                        // generate random 5 byte as identity for for the message
                        rnd.NextBytes(messageId);

                        messageAnswered = false;

                        // create message [client adr][empty][message id] and send it
                        var msg = new NetMQMessage();

                        msg.Push(messageId);
                        msg.Push(NetMQFrame.Empty);
                        msg.Push(clientId);

                        client.SendMultipartMessage(msg);
                    }
                }

                // stop poller if needed
                if (clientPoller.IsRunning)
                    clientPoller.Stop();
            }
        }
    }
}
