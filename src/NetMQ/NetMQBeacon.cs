using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Sockets;
using System.Runtime.InteropServices;

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
        public NetMQBeacon Beacon { get; }
    }

    public sealed class NetMQBeacon : IDisposable, ISocketPollable
    {
        public const int UdpFrameMax = 255;

        private const string ConfigureCommand = "CONFIGURE";
        private const string PublishCommand = "PUBLISH";
        private const string SilenceCommand = "SILENCE";
        private const string SubscribeCommand = "SUBSCRIBE";
        private const string UnsubscribeCommand = "UNSUBSCRIBE";

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

#if NET35
                    m_udpSocket.Close();
#else
                    m_udpSocket.Dispose();
#endif
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
							// because windows and unix differ in how they handle broadcast addressing this needs to be platform specific
							// on windows any interface can recieve broadcast by requesting to enable broadcast on the socket
							// on linux to recieve broadcast you must bind to the broadcast address specifically
							//bindTo = @interface.Address;
							sendTo = @interface.BroadcastAddress;
#if NET35 || NET40
							if (Environment.OSVersion.Platform==PlatformID.Unix)
#else
							if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
#endif						
							{
								bindTo = @interface.BroadcastAddress;
							}
							else
							{
								bindTo = @interface.Address;
							}
							sendTo = @interface.BroadcastAddress;

                            break;
                        }
                    }
                }

                if (bindTo != null)
                {
                    m_broadcastAddress = new IPEndPoint(sendTo, m_udpPort);
                    m_udpSocket.Bind(new IPEndPoint(bindTo, m_udpPort));
                }

                m_pipe.SendFrame(bindTo?.ToString() ?? "");
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

                using (m_poller = new NetMQPoller { m_pipe, m_pingTimer })
                {
                    m_poller.Run();
                }

                // the beacon might never been configured
#if NET35
                m_udpSocket?.Close();
#else
                m_udpSocket?.Dispose();
#endif
            }

            private void PingElapsed(object sender, NetMQTimerEventArgs e)
            {
                SendUdpFrame(m_transmit);
            }

            private void OnUdpReady(Socket socket)
            {
                var frame = ReceiveUdpFrame(out string peerName);

                // If filter is set, check that beacon matches it
                var isValid = frame.MessageSize >= m_filter?.MessageSize && Compare(frame, m_filter, m_filter.MessageSize);

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

                var bytesRead = m_udpSocket.ReceiveFrom(buffer, ref peer);
                peerName = peer.ToString();

                return new NetMQFrame(buffer, bytesRead);
            }
        }

#endregion

        private readonly NetMQActor m_actor;

        private readonly EventDelegator<NetMQBeaconEventArgs> m_receiveEvent;

        [CanBeNull] private string m_boundTo;
        [CanBeNull] private string m_hostName;
        private int m_isDisposed;

        /// <summary>
        /// Create a new NetMQBeacon.
        /// </summary>
        public NetMQBeacon()
        {
            m_actor = NetMQActor.Create(new Shim());

            void OnReceive(object sender, NetMQActorEventArgs e) => m_receiveEvent.Fire(this, new NetMQBeaconEventArgs(this));

            m_receiveEvent = new EventDelegator<NetMQBeaconEventArgs>(
                () => m_actor.ReceiveReady += OnReceive,
                () => m_actor.ReceiveReady -= OnReceive);
        }

        /// <summary>
        /// Get the host name this beacon is bound to.
        /// </summary>
        /// <remarks>
        /// This may involve a reverse DNS lookup which can take a second or two.
        /// <para/>
        /// An empty string is returned if:
        /// <list type="bullet">
        ///     <item>the beacon is not bound,</item>
        ///     <item>the beacon is bound to all interfaces,</item>
        ///     <item>an error occurred during reverse DNS lookup.</item>
        /// </list>
        /// </remarks>
        [CanBeNull]
        public string HostName
        {
            get
            {
                if (m_hostName != null)
                    return m_hostName;

                // create a copy for thread safety
                var boundTo = m_boundTo;

                if (boundTo == null)
                    return null;

                if (IPAddress.Any.ToString() == boundTo || IPAddress.IPv6Any.ToString() == boundTo)
                    return m_hostName = string.Empty;

                try
                {
#if NETSTANDARD1_3 || UAP
                    return m_hostName = Dns.GetHostEntryAsync(boundTo).Result.HostName;
#else
                    return m_hostName = Dns.GetHostEntry(boundTo).HostName;
#endif
                }
                catch
                {
                    return m_hostName = string.Empty;
                }
            }
        }

        /// <summary>
        /// Get the IP address this beacon is bound to.
        /// </summary>
        public string BoundTo => m_boundTo;

        /// <summary>
        /// Get the socket of the contained actor.
        /// </summary>
        NetMQSocket ISocketPollable.Socket => ((ISocketPollable)m_actor).Socket;

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQBeaconEventArgs> ReceiveReady
        {
            add => m_receiveEvent.Event += value;
            remove => m_receiveEvent.Event -= value;
        }

        /// <summary>
        /// Configure beacon for the specified port on all interfaces.
        /// </summary>
        /// <remarks>Blocks until the bind operation completes.</remarks>
        /// <param name="port">The UDP port to bind to.</param>
        public void ConfigureAllInterfaces(int port)
        {
            Configure(port, "*");
        }

        /// <summary>
        /// Configure beacon for the specified port and, optionally, to a specific interface.
        /// </summary>
        /// <remarks>Blocks until the bind operation completes.</remarks>
        /// <param name="port">The UDP port to bind to.</param>
        /// <param name="interfaceName">IP address of the interface to bind to. Pass empty string (the default value) to use the default interface.</param>
        public void Configure(int port, [NotNull] string interfaceName = "")
        {
            var message = new NetMQMessage();
            message.Append(ConfigureCommand);
            message.Append(interfaceName);
            message.Append(port);

            m_actor.SendMultipartMessage(message);

            m_boundTo = m_actor.ReceiveFrameString();
            m_hostName = null;
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish when interval elapsed
        /// </summary>
        /// <param name="transmit">Beacon to transmit.</param>
        /// <param name="interval">Interval to transmit beacon</param>
        /// <param name="encoding">Encoding for <paramref name="transmit"/>. Defaults to <see cref="Encoding.UTF8"/>.</param>
        public void Publish([NotNull] string transmit, TimeSpan interval, Encoding encoding = null)
        {
            Publish((encoding ?? Encoding.UTF8).GetBytes(transmit), interval);
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
        /// <param name="encoding">Encoding for <paramref name="transmit"/>. Defaults to <see cref="Encoding.UTF8"/>.</param>
        public void Publish([NotNull] string transmit, Encoding encoding = null)
        {
            Publish(transmit, TimeSpan.FromSeconds(1), encoding);
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
        /// Stop publishing beacons.
        /// </summary>
        public void Silence()
        {
            m_actor.SendFrame(SilenceCommand);
        }

        /// <summary>
        /// Subscribe to beacon messages that match the specified filter.
        /// </summary>
        /// <remarks>
        /// Beacons must prefix-match with <paramref name="filter"/>.
        /// Any previous subscription is replaced by this one.
        /// </remarks>
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
        /// Receives the next beacon message, blocking until it arrives.
        /// </summary>
        public BeaconMessage Receive()
        {
            var peerName = m_actor.ReceiveFrameString();
            var bytes = m_actor.ReceiveFrameBytes();

            return new BeaconMessage(bytes, peerName);
        }

        /// <summary>
        /// Receives the next beacon message if one is available before <paramref name="timeout"/> expires.
        /// </summary>
        /// <param name="timeout">The maximum amount of time to wait for the next beacon message.</param>
        /// <param name="message">The received beacon message.</param>
        /// <returns><c>true</c> if a beacon message was received, otherwise <c>false</c>.</returns>
        public bool TryReceive(TimeSpan timeout, out BeaconMessage message)
        {
            if (!m_actor.TryReceiveFrameString(timeout, out string peerName))
            {
                message = default(BeaconMessage);
                return false;
            }

            var bytes = m_actor.ReceiveFrameBytes();

            message = new BeaconMessage(bytes, peerName);
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_isDisposed, 1, 0) != 0)
                return;
            m_actor.Dispose();
            m_receiveEvent.Dispose();
        }

        /// <inheritdoc />
        public bool IsDisposed => m_isDisposed != 0;
    }

    /// <summary>
    /// Contents of a message received from a beacon.
    /// </summary>
    public struct BeaconMessage
    {
        /// <summary>
        /// THe beacon content as a byte array.
        /// </summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// The address of the peer that sent this message. Includes host name and port number.
        /// </summary>
        public string PeerAddress { get; }

        internal BeaconMessage(byte[] bytes, string peerAddress) : this()
        {
            Bytes = bytes;
            PeerAddress = peerAddress;
        }

        /// <summary>
        /// The beacon content as a string.
        /// </summary>
        /// <remarks>Decoded using <see cref="Encoding.UTF8"/>. Other encodings may be used with <see cref="Bytes"/> directly.</remarks>
        public string String => Encoding.UTF8.GetString(Bytes);

        /// <summary>
        /// The host name of the peer that sent this message.
        /// </summary>
        /// <remarks>This is simply the value of <see cref="PeerAddress"/> without the port number.</remarks>
        public string PeerHost
        {
            get
            {
                var i = PeerAddress.IndexOf(':');
                return i == -1 ? PeerAddress : PeerAddress.Substring(0, i);
            }
        }
    }
}
