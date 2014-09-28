using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetMQ.Actors;
using NetMQ.InProcActors;
using NetMQ.zmq;

namespace NetMQ
{
    public class NetMQBeacon : IDisposable
    {
        public const int UdpFrameMax = 255;

        public const string ConfigureCommand = "CONFIGURE";
        public const string PublishCommand = "PUBLISH";
        public const string SilenceCommand = "SILENCE";
        public const string SubscribeCommand = "SUBSCRIBE";
        public const string UnsubscribeCommand = "UNSUBSCRIBE";

        private class Agent : IShimHandler<object>
        {
            private NetMQSocket m_pipe;
            private Socket m_udpSocket;
            private int m_udpPort;
            private int m_interval;
            private EndPoint m_broadcastAddress;
            bool m_terminated;
            private long m_pingAt;
            private NetMQFrame m_transmit;
            private NetMQFrame m_filter;

            public Agent()
            {                
            }

            public void Dispose()
            {
                m_udpSocket.Dispose();
            }

            private void Configure(int port)
            {
                m_udpPort = port;

                m_udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                //  Ask operating system for broadcast permissions on socket
                m_udpSocket.EnableBroadcast = true;

                //  Allow multiple owners to bind to socket; incoming
                //  messages will replicate to each owner
                m_udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // TODO: add an option to define the listening interface
                IPAddress bindTo = IPAddress.Any;
                IPAddress sendTo = IPAddress.Broadcast;

                m_broadcastAddress = new IPEndPoint(sendTo, m_udpPort);
                m_udpSocket.Bind(new IPEndPoint(bindTo, m_udpPort));

                string hostname = m_udpSocket.LocalEndPoint.ToString();
                m_pipe.Send(hostname);
            }

            public void HandlePipe()
            {
                NetMQMessage message = m_pipe.ReceiveMessage();

                string command = message.Pop().ConvertToString();

                switch (command)
                {
                    case ConfigureCommand:
                        int port = message.Pop().ConvertToInt32();
                        Configure(port);
                        break;
                    case PublishCommand:
                        m_transmit = message.Pop();
                        m_interval = message.Pop().ConvertToInt32();

                        //  Start broadcasting immediately
                        m_pingAt = Clock.NowMs();
                        break;
                    case SilenceCommand:
                        m_transmit = null;
                        break;
                    case SubscribeCommand:
                        m_filter = message.Pop();
                        break;
                    case UnsubscribeCommand:
                        m_filter = null;
                        break;
                    case ActorKnownMessages.END_PIPE:
                        m_terminated = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private void HandleUdp()
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

            private bool Compare(NetMQFrame a, NetMQFrame b, int size)
            {
                for (int i = 0; i < size; i++)
                {
                    if (a.Buffer[i] != b.Buffer[i])
                        return false;                    
                }

                return true;
            }

            public void Initialise(object state)
            {
                
            }

            public void RunPipeline(Sockets.PairSocket shim)
            {
                m_pipe = shim;

                shim.SignalOK();

                while (!m_terminated)
                {
                    PollItem[] pollItems = new PollItem[]
                    {
                        new PollItem(m_pipe.SocketHandle, PollEvents.PollIn), 
                        new PollItem(m_udpSocket, PollEvents.PollIn), 
                    };

                    long timeout = -1;

                    if (m_transmit != null)
                    {
                        timeout = m_pingAt - Clock.NowMs();

                        if (timeout < 0)
                            timeout = 0;                        
                    }

                    ZMQ.Poll(pollItems, m_udpSocket != null ? 2 : 1, (int)timeout);

                    if (pollItems[0].ResultEvent.HasFlag(PollEvents.PollIn))
                    {
                        HandlePipe();
                    }

                    if (pollItems[1].ResultEvent.HasFlag(PollEvents.PollIn))
                    {
                        HandleUdp();
                    }

                    if (m_transmit != null && Clock.NowMs() > m_pingAt)
                    {
                        SendUdpFrame(m_transmit);
                        m_pingAt = Clock.NowMs() + m_interval;
                    }
                }

                Dispose();
            }

            private void SendUdpFrame(NetMQFrame frame)
            {
                m_udpSocket.SendTo(frame.Buffer, 0, frame.MessageSize, SocketFlags.None, m_broadcastAddress);
            }

            private NetMQFrame ReceiveUdpFrame(out string peerName)
            {
                byte[] buffer = new byte[UdpFrameMax];
                EndPoint peer = new IPEndPoint(IPAddress.Loopback, 0);

                int bytesRead = m_udpSocket.ReceiveFrom(buffer, ref peer);

                var frame = new NetMQFrame(bytesRead);
                Buffer.BlockCopy(buffer, 0, frame.Buffer, 0, bytesRead);

                peerName = peer.ToString();

                return frame;
            }           
        }

        private Agent m_agent;
        private Actor<object> m_actor;

        public NetMQBeacon(NetMQContext context)
        {
            m_agent = new Agent();
            m_actor = new Actor<object>(context, m_agent, null);
        }

        public void Configure(int port)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(ConfigureCommand);
            message.Append(port);

            m_actor.SendMessage(message);
        }

        public void Publish(string transmit, int interval = 1000)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(transmit);
            message.Append(interval);

            m_actor.SendMessage(message);
        }

        public void Silence()
        {
            m_actor.Send(SilenceCommand);
        }

        public void Subscribe(string filter)
        {
            m_actor.SendMore(SubscribeCommand).Send(filter);
        }

        public void Unsubscribe()
        {
            m_actor.Send(UnsubscribeCommand);
        }

        public void Dispose()
        {
            m_actor.Dispose();
        }
    }
}
