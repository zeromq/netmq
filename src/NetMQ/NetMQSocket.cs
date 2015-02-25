using System;
using JetBrains.Annotations;
using NetMQ.zmq;
using NetMQ.zmq.Utils;

namespace NetMQ
{
    /// <summary>
    /// NetMQSocket provides the base-class for the various message-queuing sockets used in this system,
    /// and holds a SocketBase as well as a SocketOptions.
    /// </summary>
    public abstract class NetMQSocket : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        private readonly SocketBase m_socketHandle;
        private bool m_isClosed;
        private readonly NetMQSocketEventArgs m_socketEventArgs;

        private EventHandler<NetMQSocketEventArgs> m_receiveReady;

        private EventHandler<NetMQSocketEventArgs> m_sendReady;

        private readonly Selector m_selector;

        /// <summary>
        /// Create a new NetMQSocket with the given SocketBase
        /// </summary>
        /// <param name="socketHandle">a SocketBase object to assign to the new socket</param>
        internal NetMQSocket([NotNull] SocketBase socketHandle)
        {
            m_selector = new Selector();
            m_socketHandle = socketHandle;
            Options = new SocketOptions(this);
            m_socketEventArgs = new NetMQSocketEventArgs(this);
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

        [Obsolete]
        public bool IgnoreErrors { get; set; }

        /// <summary>
        /// This event gets raised when either the SendReady or ReceiveReady event is set.
        /// </summary>
        internal event EventHandler<NetMQSocketEventArgs> EventsChanged;

        /// <summary>
        /// Get or set an integer that represents the number of errors that have accumulated.
        /// </summary>
        internal int Errors { get; set; }
        
        /// <summary>
        /// Raise the EventsChanged event.
        /// </summary>
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
        /// Get the SocketOptions of this socket.
        /// </summary>
        public SocketOptions Options { get; private set; }

        /// <summary>
        /// Get the SocketBase that this NetMQSocket contains.
        /// </summary>
        internal SocketBase SocketHandle
        {
            get { return m_socketHandle; }
        }

        NetMQSocket ISocketPollable.Socket
        {
            get { return this; }
        }

        /// <summary>
        /// Bind the socket to an address
        /// </summary>
        /// <param name="address">a string representing the address to bind this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        public void Bind([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Bind(address);            
        }

        /// <summary>
        /// Bind the socket to a random free port
        /// </summary>
        /// <param name="address">a string denoting the address of the socket, omitting the port</param>
        /// <returns>the chosen port-number</returns>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        public int BindRandomPort([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();
            
            return m_socketHandle.BindRandomPort(address);
        }

        /// <summary>
        /// Connect the socket to an address.
        /// </summary>
        /// <param name="address">a string denoting the address to connect this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        public void Connect([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Connect(address);
        }

        /// <summary>
        /// Disconnect this socket from a specific address.
        /// </summary>
        /// <param name="address">a string denoting the address to disconnect from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        public void Disconnect([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Unbind this socket from a specific address
        /// </summary>
        /// <param name="address">a string denoting the address to unbind from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        public void Unbind([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Close this socket. Do not call this if this socket has already been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">thrown if the socket is disposed</exception>
        public void Close()
        {
            if (!m_isClosed)
            {
                m_isClosed = true;

                m_socketHandle.CheckDisposed();
                m_socketHandle.Close();
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
        /// Wait until a message is ready to be received/sent from this socket or until timeout is reached.
        /// If a message is available, the ReceiveReady/SendReady event is fired.
        /// </summary>
        /// <param name="timeout">a TimeSpan that represents the timeout-period</param>
        /// <returns>true if a message was available within the timeout, false otherwise</returns>
        public bool Poll(TimeSpan timeout)
        {
            PollEvents events = GetPollEvents();

            var result = Poll(events, timeout);

            InvokeEvents(this, result);

            return result != PollEvents.None;
        }

        /// <summary>
        /// Poll this socket, which means wait for an event to happen within the given timeout period.
        /// </summary>
        /// <param name="pollEvents">the poll event(s) to listen for</param>
        /// <param name="timeout">the timeout period</param>
        /// <returns>
        /// PollEvents.None     -> no message available
        /// PollEvents.PollIn   -> no message arrived
        /// PollEvents.PollOut  -> no message to send
        /// PollEvents.Error    -> an error has occured
        /// or any combination thereof
        /// </returns>
        public PollEvents Poll(PollEvents pollEvents, TimeSpan timeout)
        {
            SelectItem[] items = {new SelectItem(SocketHandle, pollEvents)};

            m_selector.Select(items, 1, (int) timeout.TotalMilliseconds);
            return items[0].ResultEvent;
        }

        /// <summary>
        /// Return a PollEvents value that indicates which bit-flags have a corresponding listener,
        /// with PollError always set,
        /// and PollOut set based upon m_sendReady
        /// and PollIn set based upon m_receiveReady.
        /// </summary>
        /// <returns>a PollEvents value that denotes which events have a listener</returns>
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
        /// Get an available message over this socket.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="options">this controls whether to wait for the message versus returning immedately</param>
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

        /// <summary>
        /// Listen to the given endpoint for all SocketEvent events.
        /// This is the same as calling the method Monitor with a value of SocketEvent.All for the events argument.
        /// </summary>
        /// <param name="events">the specific SocketEvent events to report on. Default is SocketEvent.All if you omit this.</param>
        public void Monitor([NotNull] string endpoint, SocketEvent events = SocketEvent.All)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }

            if (endpoint == string.Empty)
            {
                throw new ArgumentException("Unable to publish socket events to an empty endpoint.", "endpoint");
            }

            m_socketHandle.CheckDisposed();

            m_socketHandle.Monitor(endpoint, events);
        }

        /// <summary>
        /// true if a message is waiting to be picked up, false otherwise
        /// </summary>
        public bool HasIn
        {
            get
            {
                PollEvents pollEvents = GetSocketOptionX<PollEvents>(ZmqSocketOptions.Events);

                return pollEvents.HasFlag(PollEvents.PollIn);
            }
        }

        /// <summary>
        /// true if a message is waiting to be sent, false otherwise
        /// </summary>
        public bool HasOut
        {
            get
            {
                PollEvents pollEvents = GetSocketOptionX<PollEvents>(ZmqSocketOptions.Events);

                return pollEvents.HasFlag(PollEvents.PollOut);
            }
        }

        internal int GetSocketOption(ZmqSocketOptions socketOptions)
        {
            m_socketHandle.CheckDisposed();

            return m_socketHandle.GetSocketOption(socketOptions);
        }

        internal T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
        {
            m_socketHandle.CheckDisposed();

            return (T)m_socketHandle.GetSocketOptionX(socketOptions);
        }

        internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
        {
            return TimeSpan.FromMilliseconds(GetSocketOption(socketOptions));
        }

        internal long GetSocketOptionLong(ZmqSocketOptions socketOptions)
        {
            return GetSocketOptionX<long>(socketOptions);
        }
     
        internal void SetSocketOption(ZmqSocketOptions socketOptions, int value)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.SetSocketOption(socketOptions, value);
        }

        internal void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
        {
            SetSocketOption(socketOptions, (int)value.TotalMilliseconds);
        }

        internal void SetSocketOption(ZmqSocketOptions socketOptions, object value)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.SetSocketOption(socketOptions, value);
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
