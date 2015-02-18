using System;
using System.Threading.Tasks;

using MajordomoProtocol;

using NetMQ;

namespace MDPBrokerProcess
{
    class Program
    {
        private static MDPBroker _Broker;
        private static bool _Verbose;

        static void Main (string[] args)
        {
            _Verbose = args.Length > 0 && args[0] == "-v";
            var keepRunning = true;

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
                                      {
                                          e.Cancel = true;
                                          keepRunning = false;
                                      };

            Console.WriteLine ("Starting Broker ...");

            //! should be handed to broker in order to hide the socket implementation
            //! what about the poller then? -> broker should handle the polling?!
            //! broker should not expose Socket
            //! broker should expose a Run/Start method which creates a poller/timer and starts the poller
            //! broker could be async with a CancellationToken which is set in Console.CancelKeyPressed
            //! that would prevent exposing internals and get rid of the ugly 'while (keepRunning)' loop
            using (_Broker = new MDPBroker ("tcp://*:5555"))
            using (var poller = new Poller ())
            {
                _Broker.Bind ();

                _Broker.Socket.ReceiveReady += ProcessReceiveMessage;

                var timer = new NetMQTimer (_Broker.HeartbeatInterval);
                // send every 'HeartbeatInterval' a heartbeat to all not expired workers
                timer.Elapsed += ProcessTimeElapsed;

                poller.AddSocket (_Broker.Socket);
                poller.AddTimer (timer);
                Console.WriteLine ("starting to listen to incoming messages ...");

                Task t = Task.Factory.StartNew (poller.Start);

                // we wait for a CTRL+C to exit
                while (keepRunning) { }

                Console.WriteLine ("Ctrl-C encountered! Shutting down broker!");

                poller.Stop ();

                // clean up
                poller.RemoveTimer (timer);
                poller.RemoveSocket (_Broker.Socket);
                // unregister event handler
                timer.Elapsed -= ProcessTimeElapsed;
                _Broker.Socket.ReceiveReady -= ProcessReceiveMessage;
            }
        }

        /// <summary>
        ///     expect from
        ///     CLIENT  ->  [sender adr][e][protocol header][service name][request]
        ///     WORKER  ->  [sender adr][e][protocol header][mdp command][reply]
        /// </summary>
        public static void ProcessReceiveMessage (object sender, NetMQSocketEventArgs e)
        {
            var msg = e.Socket.ReceiveMessage ();

            if (_Verbose)
                Console.WriteLine ("[BROKER PROCESS] Received {0}", msg);

            var senderFrame = msg.Pop ();               // [e][protocol header][service or command][data]
            var empty = msg.Pop ();                     // [protocol header][service or command][data]
            var headerFrame = msg.Pop ();               // [service or command][data]
            var header = headerFrame.ConvertToString ();

            if (header == MDPBroker.MDPClientHeader)
                _Broker.ProcessClientMessage (senderFrame, msg);
            else
                if (header == MDPBroker.MDPWorkerHeader)
                    _Broker.ProcessWorkerMessage (senderFrame, msg);
                else
                    if (_Verbose)
                        Console.WriteLine ("[BROKER PROCESS] ERROR - message with invalid protocol header!");
        }

        public static void ProcessTimeElapsed (object sender, NetMQTimerEventArgs e)
        {
            if(_Verbose)
                Console.WriteLine ("[BROKER PROCESS] Sending heartbeat");

            _Broker.SendHeartbeat ();
        }
    }
}
