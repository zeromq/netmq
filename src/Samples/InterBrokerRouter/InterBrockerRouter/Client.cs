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
        ///     this client will connect to the ROUTER socket of the broker and the PULL socket as monitor
        ///     it will send a sequence of messages and wait for max. 10s for an answer before it will
        ///     send a message via monitor
        ///     if an answer is received it will send that via monitor as well
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
            var messagedId = new byte[5];
            var poller = new Poller ();

            // if true the message has been answered
            var messageAnswered = false;

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

                // every 10 s check if message has been send, if not then send message and ext
                timer.Elapsed += (s, e) =>
                                 {
                                     if (!messageAnswered)
                                     {
                                         var msg = string.Format ("[CLIENT {0}] ERR - EXIT - lost task {1}",
                                                                  m_id,
                                                                  messagedId);
                                         // send an error message 
                                         monitor.Send (msg);
                                         // if poller is started than stop it
                                         if (poller.IsStarted)
                                             poller.Stop ();
                                         // mark the required exit
                                         exit = true;
                                     }

                                     else   // a message has arrived in time -> restart the timer
                                         timer.Enable = true;
                                 };

                client.ReceiveReady += (s, e) =>
                                       {
                                           // mark the arrival of a message
                                           messageAnswered = true;
                                           // worker is supposed to answer with our task id
                                           var reply = client.ReceiveMessage ();

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
                poller.AddSocket (client);
                poller.AddTimer (timer);

                // start poller in separate task
                Task.Factory.StartNew (poller.Start);

                while (exit == false)
                {
                    // simulate sporadic activity by randomly delaying
                    Thread.Sleep ((int) TimeSpan.FromSeconds (rnd.Next (5)).TotalMilliseconds);

                    var burst = rnd.Next (15);

                    while (burst-- >= 0 && exit != true)
                    {
                        // generate random 5 byte as identity for for the message
                        rnd.NextBytes (messagedId);

                        messageAnswered = false;

                        // [client adr][empty][message id]
                        client.Send (messagedId);
                    }
                }
            }

            // stop poller if needed
            if (poller.IsStarted)
                poller.Stop ();

            poller.Dispose ();
        }
    }
}
