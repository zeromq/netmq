using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NetMQ;
using NetMQ.Sockets;

namespace BeaconDemo
{
    /// <summary>
    /// Originally from the Docs\beacon.md documentation with small modifications for this demo
    /// </summary>
    internal class Bus
    {
        // Actor Protocol
        public const string PublishCommand = "P";
        public const string GetHostAddressCommand = "GetHostAddress";
        public const string AddedNodeCommand = "AddedNode";
        public const string RemovedNodeCommand = "RemovedNode";

        // Dead nodes timeout
        private readonly TimeSpan m_deadNodeTimeout = TimeSpan.FromSeconds(10);

        // we will use this to check if we already know about the node
        public class NodeKey
        {
            public NodeKey(string name, int port)
            {
                Name = name;
                Port = port;
                Address = string.Format("tcp://{0}:{1}", name, port);
                HostName = Dns.GetHostEntry(name).HostName;
            }

            public string Name { get; private set; }
            public int Port { get; private set; }

            public string Address { get; private set; }

            public string HostName { get; private set; }

            protected bool Equals(NodeKey other)
            {
                return string.Equals(Name, other.Name) && Port == other.Port;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((NodeKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ Port;
                }
            }

            public override string ToString()
            {
                return Address;
            }
        }

        private readonly int m_broadcastPort;

        private readonly NetMQActor m_actor;

        private PublisherSocket m_publisher;
        private SubscriberSocket m_subscriber;
        private NetMQBeacon m_beacon;
        private NetMQPoller m_poller;
        private PairSocket m_shim;
        private readonly Dictionary<NodeKey, DateTime> m_nodes; // value is the last time we "saw" this node
        private int m_randomPort;

        private Bus(int broadcastPort)
        {
            m_nodes = new Dictionary<NodeKey, DateTime>();
            m_broadcastPort = broadcastPort;
            m_actor = NetMQActor.Create(RunActor);
        }

        /// <summary>
        /// Creates a new message bus actor. All communication with the bus is
        /// through the returned <see cref="NetMQActor"/>.
        /// </summary>
        public static NetMQActor Create(int broadcastPort)
        {
            Bus node = new Bus(broadcastPort);
            return node.m_actor;
        }

        private void RunActor(PairSocket shim)
        {
            // save the shim to the class to use later
            m_shim = shim;

            // create all subscriber, publisher and beacon
            using (m_subscriber = new SubscriberSocket())
            using (m_publisher = new PublisherSocket())
            using (m_beacon = new NetMQBeacon())
            {
                // listen to actor commands
                m_shim.ReceiveReady += OnShimReady;

                // subscribe to all messages
                m_subscriber.Subscribe("");

                // we bind to a random port, we will later publish this port
                // using the beacon
                m_randomPort = m_subscriber.BindRandomPort("tcp://*");
                Console.WriteLine("Bus subscriber is bound to {0}", m_subscriber.Options.LastEndpoint);

                // listen to incoming messages from other publishers, forward them to the shim
                m_subscriber.ReceiveReady += OnSubscriberReady;

                // configure the beacon to listen on the broadcast port
                Console.WriteLine("Beacon is being configured to UDP port {0}", m_broadcastPort);
                m_beacon.Configure(m_broadcastPort);

                // publishing the random port to all other nodes
                Console.WriteLine("Beacon is publishing the Bus subscriber port {0}", m_randomPort);
                m_beacon.Publish(m_randomPort.ToString(), TimeSpan.FromSeconds(1));

                // Subscribe to all beacon on the port
                Console.WriteLine("Beacon is subscribing to all beacons on UDP port {0}", m_broadcastPort);
                m_beacon.Subscribe("");

                // listen to incoming beacons
                m_beacon.ReceiveReady += OnBeaconReady;

                // Create a timer to clear dead nodes
                NetMQTimer timer = new NetMQTimer(TimeSpan.FromSeconds(1));
                timer.Elapsed += ClearDeadNodes;

                // Create and configure the poller with all sockets and the timer
                m_poller = new NetMQPoller { m_shim, m_subscriber, m_beacon, timer };

                // signal the actor that we finished with configuration and
                // ready to work
                m_shim.SignalOK();

                // polling until cancelled
                m_poller.Run();
            }
        }

        private void OnShimReady(object sender, NetMQSocketEventArgs e)
        {
            // new actor command
            string command = m_shim.ReceiveFrameString();

            // check if we received end shim command
            if (command == NetMQActor.EndShimMessage)
            {
                // we cancel the socket which dispose and exist the shim
                m_poller.Stop();
            }
            else if (command == PublishCommand)
            {
                // it is a publish command
                // we just forward everything to the publisher until end of message
                NetMQMessage message = m_shim.ReceiveMultipartMessage();
                m_publisher.SendMultipartMessage(message);
            }
            else if (command == GetHostAddressCommand)
            {
                var address = m_beacon.BoundTo + ":" + m_randomPort;
                m_shim.SendFrame(address);
            }
        }

        private void OnSubscriberReady(object sender, NetMQSocketEventArgs e)
        {
            // we got a new message from the bus
            // let's forward everything to the shim
            NetMQMessage message = m_subscriber.ReceiveMultipartMessage();
            m_shim.SendMultipartMessage(message);
        }

        private void OnBeaconReady(object sender, NetMQBeaconEventArgs e)
        {
            // we got another beacon
            // let's check if we already know about the beacon
            var message = m_beacon.Receive();
            int port;
            int.TryParse(message.String, out port);

            NodeKey node = new NodeKey(message.PeerHost, port);

            // check if node already exist
            if (!m_nodes.ContainsKey(node))
            {
                // we have a new node, let's add it and connect to subscriber
                m_nodes.Add(node, DateTime.Now);
                m_publisher.Connect(node.Address);
                m_shim.SendMoreFrame(AddedNodeCommand).SendFrame(node.Address);
            }
            else
            {
                //Console.WriteLine("Node {0} is not a new beacon.", node);
                m_nodes[node] = DateTime.Now;
            }
        }

        private void ClearDeadNodes(object sender, NetMQTimerEventArgs e)
        {
            // create an array with the dead nodes
            var deadNodes = m_nodes.
                Where(n => DateTime.Now > n.Value + m_deadNodeTimeout)
                .Select(n => n.Key).ToArray();

            // remove all the dead nodes from the nodes list and disconnect from the publisher
            foreach (var node in deadNodes)
            {
                m_nodes.Remove(node);
                m_publisher.Disconnect(node.Address);
                m_shim.SendMoreFrame(RemovedNodeCommand).SendFrame(node.Address);
            }
        }
    }
}
