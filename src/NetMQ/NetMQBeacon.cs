using System;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ
{
    /// <summary>
    /// A NetMQBeaconEventArgs is an EventArgs that provides a property that holds a NetMQBeacon.
    /// </summary>
    public class NetMQBeaconEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQBeaconEventArgs object containing the given NetMQBeacon.
        /// </summary>
        /// <param name="beacon">the NetMQBeacon object to hold a reference to</param>
        public NetMQBeaconEventArgs([NotNull] NetMQBeacon beacon)
        {
            Beacon = beacon;
        }

        /// <summary>
        /// Get the NetMQBeacon object that this holds.
        /// </summary>
        [NotNull]
        public NetMQBeacon Beacon { get; private set; }
    }

    public class NetMQBeacon : IDisposable, ISocketPollable
    {
        public const int UdpFrameMax = 255;

        public const string ConfigureCommand = "CONFIGURE";
        public const string PublishCommand = "PUBLISH";
        public const string SilenceCommand = "SILENCE";

        /// <summary>
        /// Command to subscribe a socket to messages that have the given topic. This is valid only for Subscriber and XSubscriber sockets.
        /// </summary>
        public const string SubscribeCommand = "SUBSCRIBE";

        /// <summary>
        /// Command to un-subscribe a socket from messages that have the given topic. This is valid only for Subscriber and XSubscriber sockets.
        /// </summary>
        public const string UnsubscribeCommand = "UNSUBSCRIBE";

        #region Nested class: Shim

        private sealed class Shim : IShimHandler
        {
            private NetMQSocket m_pipe;
            private Socket m_udpSocket;
            private int m_udpPort;

            private EndPoint m_broadcastAddress;

            private NetMQFrame m_transmit;
            private NetMQFrame m_filter;
            private NetMQTimer m_pingTimer;
            private NetMQPoller m_poller;

            private void Configure([NotNull] string interfaceName, int port)
            {
                // In case the beacon was configured twice
                if (m_udpSocket != null)
                {
                    m_poller.Remove(m_udpSocket);
                    m_udpSocket.Close();
                }

                m_udpPort = port;
                m_udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                m_poller.Add(m_udpSocket, OnUdpReady);

                // Ask operating system for broadcast permissions on socket
                m_udpSocket.EnableBroadcast = true;

                // Allow multiple owners to bind to socket; incoming
                // messages will replicate to each owner
                m_udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                IPAddress bindTo = null;
                IPAddress sendTo = null;

                if (interfaceName == "*")
                {
                    bindTo = IPAddress.Any;
                    sendTo = IPAddress.Broadcast;
                }
                else if (interfaceName == "loopback")
                {
                    bindTo = IPAddress.Loopback;
                    sendTo = IPAddress.Broadcast;
                }
                else
                {
                    var interfaceCollection = new InterfaceCollection();

                    var interfaceAddress = !string.IsNullOrEmpty(interfaceName)
                        ? IPAddress.Parse(interfaceName)
                        : null;

                    foreach (var @interface in interfaceCollection)
                    {
                        if (interfaceAddress == null || @interface.Address.Equals(interfaceAddress))
                        {
                            sendTo = @interface.BroadcastAddress;
                            bindTo = @interface.Address;
                            break;
                        }
                    }
                }

                if (bindTo != null)
                {
                    m_broadcastAddress = new IPEndPoint(sendTo, m_udpPort);
                    m_udpSocket.Bind(new IPEndPoint(bindTo, m_udpPort));

                    string hostname = "";

                    try
                    {
                        if (!IPAddress.Any.Equals(bindTo) && !IPAddress.IPv6Any.Equals(bindTo))
                        {
                            var host = Dns.GetHostEntry(bindTo);
                            hostname = host != null ? host.HostName : "";
                        }
                    }
                    catch (Exception)
                    {}

                    m_pipe.SendFrame(hostname);
                }
            }

            private static bool Compare([NotNull] NetMQFrame a, [NotNull] NetMQFrame b, int size)
            {
                for (int i = 0; i < size; i++)
                {
                    if (a.Buffer[i] != b.Buffer[i])
                        return false;
                }

                return true;
            }

            public void Run(PairSocket shim)
            {
                m_pipe = shim;

                shim.SignalOK();

                m_pipe.ReceiveReady += OnPipeReady;

                m_pingTimer = new NetMQTimer(interval: TimeSpan.Zero);
                m_pingTimer.Elapsed += PingElapsed;
                m_pingTimer.Enable = false;

                m_poller = new NetMQPoller { m_pipe, m_pingTimer };

                m_poller.Run();

                // the beacon might never been configured
                if (m_udpSocket != null)
                    m_udpSocket.Close();
            }

            private void PingElapsed(object sender, NetMQTimerEventArgs e)
            {
                SendUdpFrame(m_transmit);
            }

            private void OnUdpReady(Socket socket)
            {
                string peerName;
                var frame = ReceiveUdpFrame(out peerName);

                // If filter is set, check that beacon matches it
                bool isValid = false;
                if (m_filter != null)
                {
                    if (frame.MessageSize >= m_filter.MessageSize && Compare(frame, m_filter, m_filter.MessageSize))
                    {
                        isValid = true;
                    }
                }

                // If valid, discard our own broadcasts, which UDP echoes to us
                if (isValid && m_transmit != null)
                {
                    if (frame.MessageSize == m_transmit.MessageSize && Compare(frame, m_transmit, m_transmit.MessageSize))
                    {
                        isValid = false;
                    }
                }

                // If still a valid beacon, send on to the API
                if (isValid)
                {
                    m_pipe.SendMoreFrame(peerName).SendFrame(frame.Buffer, frame.MessageSize);
                }
            }

            private void OnPipeReady(object sender, NetMQSocketEventArgs e)
            {
                NetMQMessage message = m_pipe.ReceiveMultipartMessage();

                string command = message.Pop().ConvertToString();

                switch (command)
                {
                    case ConfigureCommand:
                        string interfaceName = message.Pop().ConvertToString();
                        int port = message.Pop().ConvertToInt32();
                        Configure(interfaceName, port);
                        break;
                    case PublishCommand:
                        m_transmit = message.Pop();
                        m_pingTimer.Interval = message.Pop().ConvertToInt32();
                        m_pingTimer.Enable = true;
                        SendUdpFrame(m_transmit);
                        break;
                    case SilenceCommand:
                        m_transmit = null;
                        m_pingTimer.Enable = false;
                        break;
                    case SubscribeCommand:
                        m_filter = message.Pop();
                        break;
                    case UnsubscribeCommand:
                        m_filter = null;
                        break;
                    case NetMQActor.EndShimMessage:
                        m_poller.Stop();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private void SendUdpFrame(NetMQFrame frame)
            {
                m_udpSocket.SendTo(frame.Buffer, 0, frame.MessageSize, SocketFlags.None, m_broadcastAddress);
            }

            private NetMQFrame ReceiveUdpFrame(out string peerName)
            {
                var buffer = new byte[UdpFrameMax];
                EndPoint peer = new IPEndPoint(IPAddress.Any, 0);

                int bytesRead = m_udpSocket.ReceiveFrom(buffer, ref peer);

                var frame = new NetMQFrame(bytesRead);
                Buffer.BlockCopy(buffer, 0, frame.Buffer, 0, bytesRead);

                peerName = peer.ToString();

                return frame;
            }
        }

        #endregion

        private readonly NetMQActor m_actor;

        private readonly EventDelegator<NetMQBeaconEventArgs> m_receiveEvent;

        /// <summary>
        /// Create a new NetMQBeacon, contained within the given context.
        /// </summary>
        /// <param name="context">the NetMQContext to contain this new socket</param>
        [Obsolete("Use non context version")]
        public NetMQBeacon([NotNull] NetMQContext context)
        {
            m_actor = NetMQActor.Create(context, new Shim());

            EventHandler<NetMQActorEventArgs> onReceive = (sender, e) =>
                m_receiveEvent.Fire(this, new NetMQBeaconEventArgs(this));

            m_receiveEvent = new EventDelegator<NetMQBeaconEventArgs>(
                () => m_actor.ReceiveReady += onReceive,
                () => m_actor.ReceiveReady -= onReceive);
        }

        /// <summary>
        /// Create a new NetMQBeacon.
        /// </summary>
        public NetMQBeacon()
        {
            m_actor = NetMQActor.Create(new Shim());

            EventHandler<NetMQActorEventArgs> onReceive = (sender, e) =>
                m_receiveEvent.Fire(this, new NetMQBeaconEventArgs(this));

            m_receiveEvent = new EventDelegator<NetMQBeaconEventArgs>(
                () => m_actor.ReceiveReady += onReceive,
                () => m_actor.ReceiveReady -= onReceive);
        }



        /// <summary>
        /// Ip address the beacon is bind to
        /// </summary>
        public string Hostname { get; private set; }

        /// <summary>
        /// Get the socket of the contained actor.
        /// </summary>
        NetMQSocket ISocketPollable.Socket
        {
            get { return ((ISocketPollable)m_actor).Socket; }
        }

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQBeaconEventArgs> ReceiveReady
        {
            add { m_receiveEvent.Event += value; }
            remove { m_receiveEvent.Event -= value; }
        }

        /// <summary>
        /// Configure beacon to bind to all interfaces
        /// </summary>
        /// <param name="port">Port to bind to</param>
        public void ConfigureAllInterfaces(int port)
        {
            Configure("*", port);
        }

        /// <summary>
        /// Configure beacon to bind to default interface
        /// </summary>
        /// <param name="port">Port to bind to</param>
        public void Configure(int port)
        {
            Configure("", port);
        }

        /// <summary>
        /// Configure beacon to bind to specific interface
        /// </summary>
        /// <param name="interfaceName">One of the ip address of the interface</param>
        /// <param name="port">Port to bind to</param>
        public void Configure([NotNull] string interfaceName, int port)
        {
            var message = new NetMQMessage();
            message.Append(ConfigureCommand);
            message.Append(interfaceName);
            message.Append(port);

            m_actor.SendMultipartMessage(message);

            Hostname = m_actor.ReceiveFrameString();
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish when interval elapsed
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>
        /// <param name="interval">Interval to transmit beacon</param>
        public void Publish([NotNull] string transmit, TimeSpan interval)
        {
            var message = new NetMQMessage();
            message.Append(PublishCommand);
            message.Append(transmit);
            message.Append((int)interval.TotalMilliseconds);

            m_actor.SendMultipartMessage(message);
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish when interval elapsed
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>
        /// <param name="interval">Interval to transmit beacon</param>
        public void Publish([NotNull] byte[] transmit, TimeSpan interval)
        {
            var message = new NetMQMessage();
            message.Append(PublishCommand);
            message.Append(transmit);
            message.Append((int)interval.TotalMilliseconds);

            m_actor.SendMultipartMessage(message);
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish every second
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>
        public void Publish([NotNull] string transmit)
        {
            Publish(transmit, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish every second
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>
        public void Publish([NotNull] byte[] transmit)
        {
            Publish(transmit, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Stop publish messages
        /// </summary>
        public void Silence()
        {
            m_actor.SendFrame(SilenceCommand);
        }

        /// <summary>
        /// Subscribe to beacon messages, will replace last subscribe call
        /// </summary>
        /// <param name="filter">Beacon will be filtered by this</param>
        public void Subscribe([NotNull] string filter)
        {
            m_actor.SendMoreFrame(SubscribeCommand).SendFrame(filter);
        }

        /// <summary>
        /// Unsubscribe to beacon messages
        /// </summary>
        public void Unsubscribe()
        {
            m_actor.SendFrame(UnsubscribeCommand);
        }

        /// <summary>
        /// Blocks until a string is received. As the returning of this method is uncontrollable, it's
        /// normally safer to call <see cref="TryReceiveString"/> instead and pass a timeout.
        /// </summary>
        /// <param name="peerName">the name of the peer, which should come before the actual message, is written to this string</param>
        /// <returns>the string that was received</returns>
        [NotNull]
        public string ReceiveString(out string peerName)
        {
            peerName = m_actor.ReceiveFrameString();

            return m_actor.ReceiveFrameString();
        }

        /// <summary>
        /// Attempt to receive a message from the specified peer for the specified amount of time.
        /// </summary>
        /// <param name="timeout">The maximum amount of time the call should wait for a message before returning.</param>
        /// <param name="peerName">the name of the peer that the message comes from is written to this string</param>
        /// <param name="message">the string to write the received message into</param>
        /// <returns><c>true</c> if a message was received before <paramref name="timeout"/> elapsed,
        /// otherwise <c>false</c>.</returns>
        public bool TryReceiveString(TimeSpan timeout, out string peerName, out string message)
        {
            if (!m_actor.TryReceiveFrameString(timeout, out peerName))
            {
                message = null;
                return false;
            }

            return m_actor.TryReceiveFrameString(timeout, out message);
        }

        /// <summary>
        /// Blocks until a message is received. As the returning of this method is uncontrollable, it's
        /// normally safer to call <see cref="TryReceiveString"/> instead and pass a timeout.
        /// </summary>
        /// <param name="peerName">the name of the peer, which should come before the actual message, is written to this string</param>
        /// <returns>the byte-array of data that was received</returns>
        [NotNull]
        public byte[] Receive(out string peerName)
        {
            peerName = m_actor.ReceiveFrameString();

            return m_actor.ReceiveFrameBytes();
        }

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        /// <param name="disposing">true if managed resources are to be released</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            m_actor.Dispose();
            m_receiveEvent.Dispose();
        }
    }
}
