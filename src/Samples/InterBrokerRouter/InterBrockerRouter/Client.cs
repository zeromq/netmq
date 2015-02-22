using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NetMQ;

namespace InterBrokerRouter
{
    public class Client
    {
        private readonly string m_localFrontendAddress;
        private readonly string m_monitorAddress;
        private readonly byte m_id;

        /// <summary>
        ///     this client will connect its REQ socket to the frontend ROUTER socket of the broker and 
        ///     its PUSH socket to the the broker's PULL socket as monitor
        ///     it will send a messages and wait for max. 10s for an answer before it will
        ///     send an error message via monitor. 
        ///     if an answer is received it will send a success message via monitor as well
        /// </summary>
        /// <param name="localFrontendAddress">the local frontend address of the broker</param>
        /// <param name="monitorAddress">the monitor address of the broker</param>
        /// <param name="id">the identity of the client</param>
        public Client (string localFrontendAddress, string monitorAddress, byte id)
        {
            m_monitorAddress = monitorAddress;
            m_localFrontendAddress = localFrontendAddress;
            m_id = id;
        }

        public void Run ()
        {
            Console.WriteLine ("[CLIENT {0}] Starting", m_id);

            var rnd = new Random (m_id);
            // each request shall have an unique id in order to recognize an reply for a request
            var messageId = new byte[5];
            // create clientId for messages
            var clientId = new[] { m_id };
            // a flag to signal that an answer has arrived
            bool messageAnswered = false;
            // we use a poller because we have a socket and a timer to monitor
            var clientPoller = new Poller ();

            using (var ctx = NetMQContext.Create ())
            using (var client = ctx.CreateRequestSocket ())
            using (var monitor = ctx.CreatePushSocket ())
            {
                client.Connect (m_localFrontendAddress);
                monitor.Connect (m_monitorAddress);

                client.Options.Identity = new[] { m_id };
                var timer = new NetMQTimer ((int) TimeSpan.FromSeconds (10).TotalMilliseconds);

                // use as flag to indicate exit
                var exit = false;

                // every 10 s check if message has been received, if not then send error message and ext
                // and restart timer otherwise
                timer.Elapsed += (s, e) =>
                                 {
                                     if (messageAnswered)
                                         e.Timer.Enable = true;
                                     else
                                     {
                                         var msg = string.Format ("[CLIENT {0}] ERR - EXIT - lost message {1}", m_id, messageId);
                                         // send an error message 
                                         monitor.Send (msg);

                                         // if poller is started than stop it
                                         if (clientPoller.IsStarted)
                                             clientPoller.CancelAndJoin ();
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
                                           var reply = e.Socket.ReceiveMessage ();

                                           if (reply.FrameCount == 0)
                                           {
                                               // something went wrong
                                               monitor.Send (string.Format ("[CLIENT {0}] Received an empty message!", m_id));
                                               // mark the exit flag to ensure the exit
                                               exit = true;
                                           }
                                           else
                                           {
                                               var sb = new StringBuilder ();

                                               // create success message
                                               foreach (var frame in reply)
                                                   sb.Append ("[" + frame.ConvertToString () + "]");

                                               // send the success message
                                               monitor.Send (string.Format ("[CLIENT {0}] Received answer {1}",
                                                                            m_id,
                                                                            sb.ToString ()));
                                           }
                                       };

                // add socket & timer to poller 
                clientPoller.AddSocket (client);
                clientPoller.AddTimer (timer);

                // start poller in extra task in order to allow the continued processing
                // clientPoller.Start() -> blocking call
                var pollTask = Task.Factory.StartNew(clientPoller.PollTillCancelled);

                // if true the message has been answered
                // the 0th message is always answered
                messageAnswered = true;

                while (exit == false)
                {
                    // simulate sporadic activity by randomly delaying
                    Thread.Sleep ((int) TimeSpan.FromSeconds (rnd.Next (5)).TotalMilliseconds);

                    // only send next message if the previous one has been replied to
                    if (messageAnswered)
                    {
                        // generate random 5 byte as identity for for the message
                        rnd.NextBytes (messageId);

                        messageAnswered = false;

                        // create message [client adr][empty][message id] and send it
                        var msg = new NetMQMessage ();

                        msg.Push (messageId);
                        msg.Push (NetMQFrame.Empty);
                        msg.Push (clientId);

                        client.SendMessage (msg);
                    }
                }
            }

            // stop poller if needed
            if (clientPoller.IsStarted)
                clientPoller.CancelAndJoin ();

            clientPoller.Dispose ();
        }
    }
}
