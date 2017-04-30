using System;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Core;
#if NET40
using NetMQ.Core.Utils;
#endif

namespace NetMQ
{
    /// <summary>
    /// Abstract base class for NetMQ's different socket types.
    /// </summary>
    /// <remarks>
    /// Various options are available in this base class, though their affect can vary by socket type.
    /// </remarks>
    public abstract class NetMQSocket : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        private readonly SocketBase m_socketHandle;
        private readonly NetMQSocketEventArgs m_socketEventArgs;
        private readonly NetMQSelector m_netMqSelector;

        private EventHandler<NetMQSocketEventArgs> m_receiveReady;
        private EventHandler<NetMQSocketEventArgs> m_sendReady;
        private int m_isClosed;

        internal enum DefaultAction
        {
            Connect,
            Bind
        }

        /// <summary>
        /// Create a new NetMQSocket with the given <see cref="ZmqSocketType"/>.
        /// </summary>
        /// <param name="socketType">Type of socket to create</param>
        /// <param name="connectionString"></param>
        /// <param name="defaultAction"></param>
        internal NetMQSocket(ZmqSocketType socketType, string connectionString, DefaultAction defaultAction)
        {
            m_socketHandle = NetMQConfig.Context.CreateSocket(socketType);
            m_netMqSelector = new NetMQSelector();
            Options = new SocketOptions(this);
            m_socketEventArgs = new NetMQSocketEventArgs(this);

            Options.Linger = NetMQConfig.Linger;

            if (!string.IsNullOrEmpty(connectionString))
            {
                var endpoints = connectionString
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a));

                foreach (var endpoint in endpoints)
                {
                    switch (endpoint[0])
                    {
                        case '@':
                            Bind(endpoint.Substring(1));
                            break;
                        case '>':
                            Connect(endpoint.Substring(1));
                            break;
                        default:
                            if (defaultAction == DefaultAction.Connect)
                                Connect(endpoint);
                            else
                                Bind(endpoint);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Create a new NetMQSocket with the given <see cref="SocketBase"/>.
        /// </summary>
        /// <param name="socketHandle">a SocketBase object to assign to the new socket</param>
        internal NetMQSocket([NotNull] SocketBase socketHandle)
        {
            m_netMqSelector = new NetMQSelector();
            m_socketHandle = socketHandle;
            Options = new SocketOptions(this);
            m_socketEventArgs = new NetMQSocketEventArgs(this);
        }

        #region Events

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        /// <remarks>
        /// This event is raised when a <see cref="NetMQSocket"/> is added to a running <see cref="Poller"/>.
        /// </remarks>
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
        /// <remarks>
        /// This event is raised when a <see cref="NetMQSocket"/> is added to a running <see cref="Poller"/>.
        /// </remarks>
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

        /// <summary>
        /// Fires when either the <see cref="SendReady"/> or <see cref="ReceiveReady"/> event is set.
        /// </summary>
        internal event EventHandler<NetMQSocketEventArgs> EventsChanged;

        /// <summary>
        /// Raise the <see cref="EventsChanged"/> event.
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

        #endregion

        /// <summary>
        /// Get or set an integer that represents the number of errors that have accumulated.
        /// </summary>
        internal int Errors { get; set; }

        /// <summary>
        /// Get the <see cref="SocketOptions"/> of this socket.
        /// </summary>
        public SocketOptions Options { get; }

        /// <summary>
        /// Get the underlying <see cref="SocketBase"/>.
        /// </summary>
        internal SocketBase SocketHandle => m_socketHandle;

        NetMQSocket ISocketPollable.Socket => this;

        #region Bind, Unbind, Connect, Disconnect, Close

        /// <summary>
        /// Bind the socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string representing the address to bind this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener encountered an
        /// error during initialisation.</exception>
        public void Bind([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Bind(address);
        }

        /// <summary>Binds the specified TCP <paramref name="address"/> to an available port, assigned by the operating system.</summary>
        /// <returns>the chosen port-number</returns>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="ProtocolNotSupportedException"><paramref name="address"/> uses a protocol other than TCP.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener errored during
        /// initialisation.</exception>
        public int BindRandomPort([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            return m_socketHandle.BindRandomPort(address);
        }

        /// <summary>
        /// Connect the socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to connect this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="NetMQException">No IO thread was found.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        public void Connect([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Connect(address);
        }

        /// <summary>
        /// Disconnect this socket from <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to disconnect from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        public void Disconnect([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Unbind this socket from <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to unbind from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        public void Unbind([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Dispose()"/>.</summary>
        public void Close()
        {
            if (Interlocked.CompareExchange(ref m_isClosed, 1, 0) != 0)
                return;

            m_socketHandle.CheckDisposed();
            m_socketHandle.Close();
        }

        #endregion

        #region Polling

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
        /// PollEvents.PollIn   -> at least one message has arrived
        /// PollEvents.PollOut  -> at least one message is ready to send
        /// PollEvents.Error    -> an error has occurred
        /// or any combination thereof
        /// </returns>
        /// <exception cref="FaultException">The internal select operation failed.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public PollEvents Poll(PollEvents pollEvents, TimeSpan timeout)
        {
            NetMQSelector.Item[] items = { new NetMQSelector.Item(this, pollEvents) };

            m_netMqSelector.Select(items, 1, (long)timeout.TotalMilliseconds);
            return items[0].ResultEvent;
        }

        /// <summary>
        /// Return a <see cref="PollEvents"/> value that indicates which bit-flags have a corresponding listener,
        /// with PollError always set,
        /// and PollOut set based upon m_sendReady
        /// and PollIn set based upon m_receiveReady.
        /// </summary>
        /// <returns>a PollEvents value that denotes which events have a listener</returns>
        internal PollEvents GetPollEvents()
        {
            var events = PollEvents.PollError;

            if (m_sendReady != null)
                events |= PollEvents.PollOut;

            if (m_receiveReady != null)
                events |= PollEvents.PollIn;

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
            if (m_isClosed != 0)
                return;

            m_socketEventArgs.Init(events);

            if (events.HasIn())
                m_receiveReady?.Invoke(sender, m_socketEventArgs);

            if (events.HasOut())
                m_sendReady?.Invoke(sender, m_socketEventArgs);
        }

        #endregion

        #region Receiving messages

        /// <summary>Attempt to receive a message for the specified amount of time.</summary>
        /// <param name="msg">A reference to a <see cref="Msg"/> instance into which the received message
        /// data should be placed.</param>
        /// <param name="timeout">The maximum amount of time the call should wait for a message before returning.</param>
        /// <returns><c>true</c> if a message was received before <paramref name="timeout"/> elapsed,
        /// otherwise <c>false</c>.</returns>
        public virtual bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            return m_socketHandle.TryRecv(ref msg, timeout);
        }

        #endregion

        #region Sending messages

        /// <summary>
        /// Send a message if one is available within <paramref name="timeout"/>.
        /// </summary>
        /// <param name="msg">An object with message's data to send.</param>
        /// <param name="timeout">The maximum length of time to try and send a message. If <see cref="TimeSpan.Zero"/>, no
        /// wait occurs.</param>
        /// <param name="more">Indicate if another frame is expected after this frame</param>
        /// <returns><c>true</c> if a message was sent, otherwise <c>false</c>.</returns>
        public virtual bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            return m_socketHandle.TrySend(ref msg, timeout, more);
        }

        #endregion

        /// <summary>
        /// Listen to the given endpoint for SocketEvent events.
        /// </summary>
        /// <param name="endpoint">A string denoting the endpoint to monitor</param>
        /// <param name="events">The specific <see cref="SocketEvents"/> events to report on. Defaults to <see cref="SocketEvents.All"/> if omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="endpoint"/> cannot be empty or whitespace.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        /// <exception cref="ProtocolNotSupportedException">The protocol of <paramref name="endpoint"/> is not supported.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="NetMQException">Maximum number of sockets reached.</exception>
        public void Monitor([NotNull] string endpoint, SocketEvents events = SocketEvents.All)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentException("Cannot be empty.", nameof(endpoint));

            m_socketHandle.CheckDisposed();

            m_socketHandle.Monitor(endpoint, events);
        }

        #region Socket options

        /// <summary>
        /// Get whether a message is waiting to be picked up (<c>true</c> if there is, <c>false</c> if there is none).
        /// </summary>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public bool HasIn => GetSocketOptionX<PollEvents>(ZmqSocketOption.Events).HasIn();

        /// <summary>
        /// Get whether a message is waiting to be sent.
        /// </summary>
        /// <remarks>
        /// This is <c>true</c> if at least one message is waiting to be sent, <c>false</c> if there is none.
        /// </remarks>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public bool HasOut => GetSocketOptionX<PollEvents>(ZmqSocketOption.Events).HasOut();

        /// <summary>
        /// Get the integer-value of the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>an integer that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal int GetSocketOption(ZmqSocketOption option)
        {
            m_socketHandle.CheckDisposed();

            return m_socketHandle.GetSocketOption(option);
        }

        /// <summary>
        /// Get the (generically-typed) value of the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>an object of the given type, that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal T GetSocketOptionX<T>(ZmqSocketOption option)
        {
            m_socketHandle.CheckDisposed();

            return (T)m_socketHandle.GetSocketOptionX(option);
        }

        /// <summary>
        /// Get the <see cref="TimeSpan"/> value of the specified ZmqSocketOption.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>a TimeSpan that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOption option)
        {
            return TimeSpan.FromMilliseconds(GetSocketOption(option));
        }

        /// <summary>
        /// Get the 64-bit integer-value of the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>a long that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal long GetSocketOptionLong(ZmqSocketOption option)
        {
            return GetSocketOptionX<long>(option);
        }

        /// <summary>
        /// Assign the given integer value to the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="value">an integer that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal void SetSocketOption(ZmqSocketOption option, int value)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.SetSocketOption(option, value);
        }

        /// <summary>
        /// Assign the given TimeSpan to the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="value">a TimeSpan that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal void SetSocketOptionTimeSpan(ZmqSocketOption option, TimeSpan value)
        {
            SetSocketOption(option, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Assign the given Object value to the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="value">an object that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal void SetSocketOption(ZmqSocketOption option, object value)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.SetSocketOption(option, value);
        }

        #endregion

        #region IDisposable

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Close"/>.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Close"/>.</summary>
        /// <param name="disposing">true if releasing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Close();
        }

        /// <inheritdoc />
        public bool IsDisposed => m_isClosed != 0;

        #endregion
    }
}
