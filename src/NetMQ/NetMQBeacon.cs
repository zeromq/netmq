using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ
{
    public class NetMQBeaconEventArgs : EventArgs
    {
        public NetMQBeaconEventArgs(NetMQBeacon beacon)
        {
            Beacon = beacon;
        }

        public NetMQBeacon Beacon { get; private set; }
    }

    public class NetMQBeacon : IDisposable, ISocketPollable
    {
        public const int UdpFrameMax = 255;

        public const string ConfigureCommand = "CONFIGURE";
        public const string PublishCommand = "PUBLISH";
        public const string SilenceCommand = "SILENCE";
        public const string SubscribeCommand = "SUBSCRIBE";
        public const string UnsubscribeCommand = "UNSUBSCRIBE";

        private class Shim : IShimHandler
        {
            private NetMQSocket m_pipe;
            private Socket m_udpSocket;
            private int m_udpPort;
            
            private EndPoint m_broadcastAddress;

            private NetMQFrame m_transmit;
            private NetMQFrame m_filter;
            private NetMQTimer m_pingTimer;
            private Poller m_poller;

            public Shim()
            {
            }

            private void Configure(string interfaceName, int port)
            {
                // incase the beacon was configured twice
                if (m_udpSocket != null)
                {
                    m_poller.RemovePollInSocket(m_udpSocket);
                    m_udpSocket.Close();                                    
                }

                m_udpPort = port;
                m_udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                m_poller.AddPollInSocket(m_udpSocket, OnUdpReady);                

                //  Ask operating system for broadcast permissions on socket
                m_udpSocket.EnableBroadcast = true;

                //  Allow multiple owners to bind to socket; incoming
                //  messages will replicate to each owner
                m_udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                IPAddress bindTo = null;
                IPAddress sendTo = null;

                if (interfaceName.Equals("*"))
                {
                    bindTo = IPAddress.Any;
                    sendTo = IPAddress.Broadcast;
                }
                else
                {
                    InterfaceCollection interfaceCollection = new InterfaceCollection();

                    IPAddress interfaceAddress = null;

                    if (!string.IsNullOrEmpty(interfaceName))
                    {
                        interfaceAddress = IPAddress.Parse(interfaceName);
                    }

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
                        var host = Dns.GetHostEntry(bindTo);
                        hostname = host != null ? host.HostName : "";
                    }
                    catch (Exception)
                    {
                    }

                    m_pipe.Send(hostname);
                }
            }

            private bool Compare(NetMQFrame a, NetMQFrame b, int size)
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

                m_pingTimer = new NetMQTimer(0);
                m_pingTimer.Elapsed += PingElapsed;
                m_pingTimer.Enable = false;

                m_poller = new Poller();
                m_poller.AddSocket(m_pipe);                
                m_poller.AddTimer(m_pingTimer);

                m_poller.PollTillCancelled();

                // the beacon might never been configured
                if (m_udpSocket != null)
                {
                    m_udpSocket.Close();
                }                
            }

            private void PingElapsed(object sender, NetMQTimerEventArgs e)
            {
                SendUdpFrame(m_transmit);
            }

            private void OnUdpReady(Socket socket)
            {
                string peerName;
                var frame = ReceiveUdpFrame(out peerName);

                //  If filter is set, check that beacon matches it
                bool isValid = false;
                if (m_filter != null)
                {
                    if (frame.MessageSize >= m_filter.MessageSize && Compare(frame, m_filter, m_filter.MessageSize))
                    {
                        isValid = true;
                    }
                }

                //  If valid, discard our own broadcasts, which UDP echoes to us
                if (isValid && m_transmit != null)
                {
                    if (frame.MessageSize == m_transmit.MessageSize && Compare(frame, m_filter, m_filter.MessageSize))
                    {
                        isValid = false;
                    }
                }

                //  If still a valid beacon, send on to the API
                if (isValid)
                {
                    m_pipe.SendMore(peerName).Send(frame.Buffer, frame.MessageSize);
                }
            }

            private void OnPipeReady(object sender, NetMQSocketEventArgs e)
            {
                NetMQMessage message = m_pipe.ReceiveMessage();

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
                        m_poller.Stop(false);
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
                byte[] buffer = new byte[UdpFrameMax];
                EndPoint peer = new IPEndPoint(IPAddress.Any, 0);

                int bytesRead = m_udpSocket.ReceiveFrom(buffer, ref peer);

                var frame = new NetMQFrame(bytesRead);
                Buffer.BlockCopy(buffer, 0, frame.Buffer, 0, bytesRead);

                peerName = peer.ToString();

                return frame;
            }
        }

        private Shim m_shim;
        private NetMQActor m_actor;

        private EventDelegatorHelper<NetMQBeaconEventArgs> m_receiveEventHelper;

        public NetMQBeacon(NetMQContext context)
        {
            m_shim = new Shim();
            m_actor = NetMQActor.Create(context, m_shim);

            m_receiveEventHelper = new EventDelegatorHelper<NetMQBeaconEventArgs>(() => m_actor.ReceiveReady += OnReceiveReady,
                () => m_actor.ReceiveReady -= OnReceiveReady);
        }

        /// <summary>
        /// Ip address the beacon is bind to
        /// </summary>
        public string Hostname { get; private set; }

        NetMQSocket ISocketPollable.Socket
        {
            get { return (m_actor as ISocketPollable).Socket; }
        }

        public event EventHandler<NetMQBeaconEventArgs> ReceiveReady
        {
            add
            {
                m_receiveEventHelper.Event += value;
            }
            remove
            {
                m_receiveEventHelper.Event -= value;
            }
        }

        private void OnReceiveReady(object sender, NetMQActorEventArgs args)
        {
            m_receiveEventHelper.Fire(this, new NetMQBeaconEventArgs(this));
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
        public void Configure(string interfaceName, int port)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(ConfigureCommand);
            message.Append(interfaceName);
            message.Append(port);

            m_actor.SendMessage(message);

            Hostname = m_actor.ReceiveString();
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish when interval elapsed 
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>
        /// <param name="interval">Interval to transmit beacon</param>
        public void Publish(string transmit, TimeSpan interval)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(PublishCommand);
            message.Append(transmit);
            message.Append((int)interval.TotalMilliseconds);

            m_actor.SendMessage(message);
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish when interval elapsed 
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>
        /// <param name="interval">Interval to transmit beacon</param>
        public void Publish(byte[] transmit, TimeSpan interval)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(PublishCommand);
            message.Append(transmit);
            message.Append((int)interval.TotalMilliseconds);

            m_actor.SendMessage(message);
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish every second
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>        
        public void Publish(string transmit)
        {
            Publish(transmit, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Publish beacon immediately and continue to publish every second
        /// </summary>
        /// <param name="transmit">Beacon to transmit</param>        
        public void Publish(byte[] transmit)
        {
            Publish(transmit, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Stop publish messages
        /// </summary>
        public void Silence()
        {
            m_actor.Send(SilenceCommand);
        }

        /// <summary>
        /// Subscribe to beacon messages, will replace last subscribe call
        /// </summary>
        /// <param name="filter">Beacon will be filtered by this</param>
        public void Subscribe(string filter)
        {
            m_actor.SendMore(SubscribeCommand).Send(filter);
        }

        /// <summary>
        /// Unsubscribe to beacon messages
        /// </summary>
        public void Unsubscribe()
        {
            m_actor.Send(UnsubscribeCommand);
        }

        public string ReceiveString(out string peerName)
        {
            peerName = m_actor.ReceiveString();

            return m_actor.ReceiveString();
        }

        public byte[] Receive(out string peerName)
        {
            peerName = m_actor.ReceiveString();

            return m_actor.Receive();
        }

        public void Dispose()
        {
            m_actor.Dispose();
        }
    }
}
