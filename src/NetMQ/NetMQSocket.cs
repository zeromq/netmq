// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using NetMQ.zmq;


namespace NetMQ
{
    public abstract class NetMQSocket : IOutgoingSocket, IReceivingSocket, IDisposable
    {
        readonly SocketBase m_socketHandle;
        private bool m_isClosed;
        private NetMQSocketEventArgs m_socketEventArgs;

        private EventHandler<NetMQSocketEventArgs> m_receiveReady;

        private EventHandler<NetMQSocketEventArgs> m_sendReady;

        /// <summary>
        /// Create a new NetMQSocket based upon the given SocketBase
        /// </summary>
        /// <param name="socketHandle">a SocketBase object to assign to the new socket</param>
        protected NetMQSocket(SocketBase socketHandle)
        {
            m_socketHandle = socketHandle;
            Options = new SocketOptions(this);
            m_socketEventArgs = new NetMQSocketEventArgs(this);

            IgnoreErrors = false;
            Errors = 0;
        }

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQSocketEventArgs> ReceiveReady
        {
            add
            {
                m_receiveReady += value;
                InvokeEventsChanged();
            }
            remove
            {
                m_receiveReady -= value;
                InvokeEventsChanged();
            }
        }

        /// <summary>
        /// This event occurs when at least one message may be sent via the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQSocketEventArgs> SendReady
        {
            add
            {
                m_sendReady += value;
                InvokeEventsChanged();
            }
            remove
            {
                m_sendReady -= value;
                InvokeEventsChanged();
            }
        }

        public bool IgnoreErrors { get; set; }

        internal event EventHandler<NetMQSocketEventArgs> EventsChanged;

        internal int Errors { get; set; }


        private void InvokeEventsChanged()
        {
            var temp = EventsChanged;

            if (temp != null)
            {
                m_socketEventArgs.Init(PollEvents.None);
                temp(this, m_socketEventArgs);
            }
        }

        /// <summary>
        /// Get the options of this socket.
        /// </summary>
        public SocketOptions Options { get; private set; }

        internal SocketBase SocketHandle
        {
            get
            {
                return m_socketHandle;
            }
        }

        /// <summary>
        /// Bind the socket to an address
        /// </summary>
        /// <param name="address">The address to bind this socket to</param>
        public void Bind(string address)
        {
            ZMQ.Bind(m_socketHandle, address);
        }

        /// <summary>
        /// Connect the socket to an address
        /// </summary>
        /// <param name="address">the address to connect this socket to</param>
        public void Connect(string address)
        {
            ZMQ.Connect(m_socketHandle, address);
        }

        /// <summary>
        /// Disconnect this socket from a specific address.
        /// </summary>
        /// <param name="address">the address to disconnect from</param>
        public void Disconnect(string address)
        {
            ZMQ.Disconnect(m_socketHandle, address);
        }

        /// <summary>
        /// Unbind this socket from a specific address.
        /// </summary>
        /// <param name="address">the address to unbind from</param>
        public void Unbind(string address)
        {
            ZMQ.Unbind(m_socketHandle, address);
        }

        /// <summary>
        /// Close this socket.
        /// </summary>
        /// <remarks>
        /// There is no harm in calling this method while the socket is already closed.
        /// </remarks>
        public void Close()
        {
            if (!m_isClosed)
            {
                m_isClosed = true;
                ZMQ.Close(m_socketHandle);
            }
        }

        /// <summary>
        /// Wait until a message is ready to be received from the socket.
        /// </summary>
        public void Poll()
        {
            Poll(TimeSpan.FromMilliseconds(-1));
        }

        /// <summary>
        /// Wait until a message is ready to be received from the socket or until timeout is reached
        /// </summary>
        /// <param name="timeout">a TimeSpan that represents the timeout-period</param>
        /// <returns>true if the result-event is other than PollEvents.None</returns>
        public bool Poll(TimeSpan timeout)
        {
            PollEvents events = GetPollEvents();

            PollItem item = new PollItem(m_socketHandle, events);

            PollItem[] items = new PollItem[] { item };

            ZMQ.Poll(items, (int)timeout.TotalMilliseconds);

            if (item.ResultEvent.HasFlag(PollEvents.PollError) && !IgnoreErrors)
            {
                Errors++;

                if (Errors > 1)
                {
                    throw new ErrorPollingException("Error while polling", this);
                }
            }
            else
            {
                Errors = 0;
            }

            InvokeEvents(this, item.ResultEvent);

            return items[0].ResultEvent != PollEvents.None;
        }

        /// <summary>
        /// Return a PollEvents value that indicates which bit-flags have a corresponding listener,
        /// with PollError always set,
        /// and PollOut set based upon m_sendReady
        /// and PollIn set based upon m_receiveReady.
        /// </summary>
        /// <returns>a PollEvents value</returns>
        internal PollEvents GetPollEvents()
        {
            PollEvents events = PollEvents.PollError;

            if (m_sendReady != null)
            {
                events |= PollEvents.PollOut;
            }

            if (m_receiveReady != null)
            {
                events |= PollEvents.PollIn;
            }

            return events;
        }

        /// <summary>
        /// Unless this socket is closed,
        /// based upon the given PollEvents - raise the m_receiveReady event if PollIn is set,
        /// and m_sendReady if PollOut is set.
        /// </summary>
        /// <param name="sender">what to use as the source of the events</param>
        /// <param name="events">the given PollEvents that dictates when of the two events to raise</param>
        internal void InvokeEvents(object sender, PollEvents events)
        {
            if (!m_isClosed)
            {
                m_socketEventArgs.Init(events);

                if (events.HasFlag(PollEvents.PollIn))
                {
                    var temp = m_receiveReady;
                    if (temp != null)
                    {
                        temp(sender, m_socketEventArgs);
                    }
                }

                if (events.HasFlag(PollEvents.PollOut))
                {
                    var temp = m_sendReady;
                    if (temp != null)
                    {
                        temp(sender, m_socketEventArgs);
                    }
                }
            }
        }

        /// <summary>
        /// Receive a message over this socket. This is the internal helper-method that the other Receive methods use.
        /// </summary>
        /// <param name="options">this controls whether to wait for the message versus returning immedately</param>
        /// <param name="hasMore">this get set to true by the receive-operation if the incoming is part of a multi-part message and more are coming</param>
        /// <returns>a Msg object containing what was received</returns>
        protected internal virtual Msg ReceiveInternal(SendReceiveOptions options, out bool hasMore)
        {
            var msg = ZMQ.Recv(m_socketHandle, options);

            hasMore = msg.HasMore;

            return msg;
        }

        /// <summary>
        /// Receive a message over this socket, as a byte-array.
        /// </summary>
        /// <param name="dontWait">if true - return immediately without waiting</param>
        /// <param name="hasMore">this get set to true by the receive-operation if the incoming is part of a multi-part message and more are coming</param>
        /// <returns>the data that was received as a byte-array</returns>
        public byte[] Receive(bool dontWait, out bool hasMore)
        {
            return ReceiveInternal(dontWait ? SendReceiveOptions.DontWait : SendReceiveOptions.None, out hasMore).Data;
        }

        /// <summary>
        /// Transmit the given NetMQMessage over this socket, blocking to wait for transmission.
        /// This is the same as calling SendMessage(NetMQMessage, bool dontWait) with dontWait set to false.
        /// </summary>
        /// <param name="message">the NetMQMessage to send</param>
        public void SendMessage(NetMQMessage message)
        {
            SendMessage(message, false);
        }

        /// <summary>
        /// Transmit the given NetMQMessage over this socket, either blocking to wait for transmission (if dontWait is false) or not blocking (if dontWait is true).
        /// </summary>
        /// <param name="message">the NetMQMessage to send</param>
        /// <param name="dontWait">if true - return immediately without waiting</param>
        public void SendMessage(NetMQMessage message, bool dontWait)
        {
            for (int i = 0; i < message.FrameCount - 1; i++)
            {
                Send(message[i].Buffer, message[i].MessageSize, dontWait, true);
            }

            Send(message.Last.Buffer, message.Last.MessageSize, dontWait, false);
        }

        /// <summary>
        /// Transmit a specified number of bytes of the given byte-array data over this socket,
        /// with the given SendReceiveOptions that control whether to wait and whether this is part of a multi-part message and more is coming.
        /// </summary>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to send</param>
        /// <param name="options">this controls whether to wait and whether this is part of a multi-part message and more is coming</param>
        public virtual void Send(byte[] data, int length, SendReceiveOptions options)
        {
            Msg msg = new Msg(data, length, Options.CopyMessages);

            ZMQ.Send(m_socketHandle, msg, options);
        }

#if !PRE_4
        /// <summary>
        /// Transmit a specified number of bytes of the given byte-array data over this socket,
        /// with the default values of false for the dontWait and sendMore parameters.
        /// </summary>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to send</param>
        /// <param name="dontWait">if true - return immediately without waiting</param>
        /// <param name="sendMore">if true, this indicates that this is one part of a multi-part message and more parts are coming</param>
        public void Send(byte[] data, int length, bool dontWait = false, bool sendMore = false)
        {
            SendReceiveOptions sendReceiveOptions = SendReceiveOptions.None;

            if (dontWait)
            {
                sendReceiveOptions |= SendReceiveOptions.DontWait;
            }

            if (sendMore)
            {
                sendReceiveOptions |= SendReceiveOptions.SendMore;
            }

            Send(data, length, sendReceiveOptions);
        }

#else // .NET 3.5 and earlier did not have default paramter values.

        /// <summary>
        /// Transmit the given byte-array data over this socket,
        /// with the default values of false for the dontWait and sendMore parameters.
        /// </summary>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to send</param>
		public void Send(byte[] data, int length)
		{
            Send(data, length, false, false);
        }

        /// <summary>
        /// Transmit a specified number of bytes of the given byte-array data over this socket.
        /// </summary>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to send</param>
        /// <param name="dontWait">if true - return immediately without waiting</param>
        /// <param name="sendMore">if true, this indicates that this is one part of a multi-part message and more parts are coming</param>
		public void Send(byte[] data, int length, bool dontWait, bool sendMore)
		{
			SendReceiveOptions sendReceiveOptions = SendReceiveOptions.None;

			if (dontWait)
			{
				sendReceiveOptions |= SendReceiveOptions.DontWait;
			}

			if (sendMore)
			{
				sendReceiveOptions |= SendReceiveOptions.SendMore;
			}

			Send(data, length, sendReceiveOptions);
		}
#endif

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

        /// <summary>
        /// Listen to the given endpoint for all SocketEvent events.
        /// This is the same as calling the method Monitor with a value of SocketEvent.All for the events argument.
        /// </summary>
        /// <param name="endpoint"></param>
        public void Monitor(string endpoint)
        {
            Monitor(endpoint, SocketEvent.All);
        }

        /// <summary>
        /// Listen to the given endpoint address for socket state-change events.   CBL And do what?
        /// </summary>
        /// <param name="endpoint">the endpoint to monitor for state-change events</param>
        /// <param name="events">the specific SocketEvent events to report on. Default is SocketEvent.All if you omit this.</param>
        public void Monitor(string endpoint, SocketEvent events)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to publish socket events to an empty endpoint.", "endpoint");
            }

            ZMQ.SocketMonitor(SocketHandle, endpoint, events);
        }

        internal int GetSocketOption(ZmqSocketOptions socketOptions)
        {
            return ZMQ.GetSocketOption(m_socketHandle, socketOptions);
        }

        internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
        {
            return TimeSpan.FromMilliseconds(ZMQ.GetSocketOption(m_socketHandle, socketOptions));
        }

        internal long GetSocketOptionLong(ZmqSocketOptions socketOptions)
        {
            return (long)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
        }

        internal T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
        {
            return (T)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
        }

        internal void SetSocketOption(ZmqSocketOptions socketOptions, int value)
        {
            ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
        }

        internal void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
        {
            ZMQ.SetSocketOption(m_socketHandle, socketOptions, (int)value.TotalMilliseconds);
        }

        internal void SetSocketOption(ZmqSocketOptions socketOptions, object value)
        {
            ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
        }

        /// <summary>
        /// Release this socket's resources (by simply closing it).
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
