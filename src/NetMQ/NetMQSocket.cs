using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NetMQ.Core;

namespace NetMQ
{
    public abstract class NetMQSocket : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        readonly SocketBase m_socketHandle;
        private bool m_isClosed = false;
        private NetMQSocketEventArgs m_socketEventArgs;

        internal NetMQSocket(SocketBase socketHandle)
        {
            m_socketHandle = socketHandle;
            Options = new SocketOptions(this);
            m_socketEventArgs = new NetMQSocketEventArgs(this);
        }

        /// <summary>
        /// Occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQSocketEventArgs> ReceiveReady;
       

        /// <summary>
        /// Occurs when at least one message may be sent via the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQSocketEventArgs> SendReady;
       
        [Obsolete("Ignore errors is not used anymore")]
        public bool IgnoreErrors { get; set; }      

        /// <summary>
        /// Set the options of the socket
        /// </summary>
        public SocketOptions Options { get; private set; }

        internal SocketBase SocketHandle
        {
            get
            {
                return m_socketHandle;
            }
        }

        NetMQSocket ISocketPollable.Socket
        {
            get { return this; }
        }

        /// <summary>
        /// Bind the socket to an address
        /// </summary>
        /// <param name="address">The address of the socket</param>
        public void Bind(string address)
        {
            m_socketHandle.Bind(address);
        }

        /// <summary>
        /// Bind the socket to a random free port
        /// </summary>
        /// <param name="address">The address of the socket, omit the port</param>
        /// <returns>Chosen port number</returns>
        public int BindRandomPort(string address)
        {
            return m_socketHandle.BindRandomPort(address);
        }

        /// <summary>
        /// Connect the socket to an address
        /// </summary>
        /// <param name="address">Address to connect to</param>
        public void Connect(string address)
        {
            m_socketHandle.Connect(address);
        }

        /// <summary>
        /// Disconnect the socket from specific address
        /// </summary>
        /// <param name="address">The address to disconnect from</param>
        public void Disconnect(string address)
        {
            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Unbind the socket from specific address
        /// </summary>
        /// <param name="address">The address to unbind from</param>
        public void Unbind(string address)
        {
            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Close the socket
        /// </summary>
        public void Close()
        {
            if (!m_isClosed)
            {
                m_isClosed = true;
                m_socketHandle.Close();
            }
        }

        /// <summary> Wait until message is ready to be received from the socket. </summary>
        public void Poll()
        {
            Poll(TimeSpan.FromMilliseconds(-1));
        }

        /// <summary>
        /// Wait until message is ready to be received from the socket or until timeout is reached
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool Poll(TimeSpan timeout)
        {
            PollEvents events = GetPollEvents();

            var result = SocketHandle.Poll(events, (int)timeout.TotalMilliseconds);

            m_socketEventArgs.Init(events);

            if (result.HasFlag(PollEvents.PollIn))
            {
                var temp = ReceiveReady;
                if (temp != null)
                {
                    temp(this, m_socketEventArgs);
                }
            }

            if (result.HasFlag(PollEvents.PollOut))
            {
                var temp = SendReady;
                if (temp != null)
                {
                    temp(this, m_socketEventArgs);
                }
            }

            return result != PollEvents.None;
        }

        internal PollEvents GetPollEvents()
        {
            PollEvents events = PollEvents.None;

            if (SendReady != null)
            {
                events |= PollEvents.PollOut;
            }

            if (ReceiveReady != null)
            {
                events |= PollEvents.PollIn;
            }

            return events;
        }

        internal bool InvokeEvents(object sender)
        {
            if (!m_isClosed)
            {                
                PollEvents events = SocketHandle.GetEvents(GetPollEvents());
                                
                m_socketEventArgs.Init(events);

                var receiveReady = ReceiveReady;
                if (receiveReady!= null && events.HasFlag(PollEvents.PollIn))
                {
                    receiveReady(sender, m_socketEventArgs);
                }

                var sendReady = SendReady;
                if (sendReady != null && events.HasFlag(PollEvents.PollOut))
                {
                    sendReady(sender, m_socketEventArgs);
                }

                return events != PollEvents.None;
            }

            return false;
        }

        public virtual void Receive(ref Msg msg, SendReceiveOptions options)
        {
            m_socketHandle.Recv(ref msg, options);
        }

        public virtual void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_socketHandle.Send(ref msg, options);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        public void Monitor(string endpoint, SocketEvent events = SocketEvent.All)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to publish socket events to an empty endpoint.", "endpoint");
            }

            SocketHandle.Monitor(endpoint, events);
        }

        public bool HasIn
        {
            get
            {
                PollEvents pollEvents = (PollEvents)
                    m_socketHandle.GetSocketOptionX(ZmqSocketOptions.Events);

                return pollEvents.HasFlag(PollEvents.PollIn);
            }
        }

        public bool HasOut
        {
            get
            {
                PollEvents pollEvents = (PollEvents)
                    SocketHandle.GetSocketOptionX(ZmqSocketOptions.Events);

                return pollEvents.HasFlag(PollEvents.PollOut);
            }
        }

        internal int GetSocketOption(ZmqSocketOptions socketOptions)
        {
            return m_socketHandle.GetSocketOption(socketOptions);
        }

        internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
        {
            return TimeSpan.FromMilliseconds(m_socketHandle.GetSocketOption(socketOptions));
        }

        internal long GetSocketOptionLong(ZmqSocketOptions socketOptions)
        {
            return (long)m_socketHandle.GetSocketOptionX(socketOptions);
        }

        internal T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
        {
            return (T)m_socketHandle.GetSocketOptionX(socketOptions);
        }

        internal void SetSocketOption(ZmqSocketOptions socketOptions, int value)
        {
            m_socketHandle.SetSocketOption(socketOptions, value);
        }

        internal void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
        {
            m_socketHandle.SetSocketOption(socketOptions, (int)value.TotalMilliseconds);
        }

        internal void SetSocketOption(ZmqSocketOptions socketOptions, object value)
        {
            m_socketHandle.SetSocketOption(socketOptions, value);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
