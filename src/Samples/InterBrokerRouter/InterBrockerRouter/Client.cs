using System;
using System.Threading;
using System.Threading.Tasks;

using NetMQ;

namespace InterBrokerRouter
{
    public class Client
    {
        private readonly NetMQContext _ctx;
        private readonly string _localFrontendAddress;
        private readonly string _monitorAddress;

        public Client (NetMQContext ctx, string localFrontendAddress, string monitorAddress)
        {
            _ctx = ctx;
            _monitorAddress = monitorAddress;
            _localFrontendAddress = localFrontendAddress;
        }

        public void Run ()
        {
            var rnd = new Random ();
            var messagedId = new byte[5];
            var poller = new Poller ();
            var id = (byte) rnd.Next (255);

            // if true the message has been answered
            var messageAnswered = false;

            using (var client = _ctx.CreateRequestSocket ())
            using (var monitor = _ctx.CreatePushSocket ())
            {
                client.Connect (_localFrontendAddress);
                monitor.Connect (_monitorAddress);

                client.Options.Identity = new[] { id };
                var timer = new NetMQTimer ((int) TimeSpan.FromSeconds (10).TotalMilliseconds);

                // use as flag to indicate exit
                var exit = false;

                // every 10 s check if message has been send, if not then send message and ext
                timer.Elapsed += (s, e) =>
                                 {
                                     if (!messageAnswered)
                                     {
                                         var msg = string.Format ("[CLIENT {0}] ERR - EXIT - lost task {1}",
                                                                  client.Options.Identity,
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
                                           var reply = client.Receive ();

                                           if (reply.Length == 0)
                                           {
                                               // something went wrong
                                               monitor.Send (string.Format ("[CLIENT {0}]: Received an empty message!",
                                                                            id));
                                               // mark the exit flag to ensure the exit
                                               exit = true;
                                           }
                                           else
                                               // send the success message
                                               monitor.Send (string.Format ("[CLIENT {0}: Received answer [{1}][{2}][{3}]",
                                                                            reply[0],
                                                                            reply[1],
                                                                            reply[2]));
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

                        client.Send (messagedId);
                    }
                }
            }
        }
    }
}
