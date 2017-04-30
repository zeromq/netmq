using System;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ
{
    #region IShimHandler

    /// <summary>
    /// An IShimHandler provides a Run(PairSocket) method.
    /// </summary>
    public interface IShimHandler
    {
        /// <summary>
        /// Execute whatever action this <c>IShimHandler</c> represents against the given shim.
        /// </summary>
        /// <param name="shim"></param>
        void Run([NotNull] PairSocket shim);
    }

    #endregion

    #region NetMQActorEventArgs

    /// <summary>
    /// This is an EventArgs that provides an Actor property.
    /// </summary>
    public class NetMQActorEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new NetMQActorEventArgs with the given NetMQActor.
        /// </summary>
        /// <param name="actor">the NetMQActor for this exception to reference</param>
        public NetMQActorEventArgs([NotNull] NetMQActor actor)
        {
            Actor = actor;
        }

        /// <summary>
        /// Get the NetMQActor that this exception references.
        /// </summary>
        [NotNull]
        public NetMQActor Actor { get; }
    }

    #endregion

    #region Delegates

    /// <summary>
    /// This delegate represents the action for this actor to execute.
    /// </summary>
    /// <param name="shim">the <seealso cref="PairSocket"/> that is the shim to execute this action</param>
    public delegate void ShimAction(PairSocket shim);

    /// <summary>
    /// This delegate represents the action for this actor to execute - along with a state-information object.
    /// </summary>
    /// <typeparam name="T">the type to use for the state-information object</typeparam>
    /// <param name="shim">the <seealso cref="PairSocket"/> that is the shim to execute this action</param>
    /// <param name="state">the state-information that the action will use</param>
    public delegate void ShimAction<in T>(PairSocket shim, T state);

    #endregion

    /// <summary>
    /// The Actor represents one end of a two-way pipe between 2 PairSocket(s). Where
    /// the actor may be passed messages, that are sent to the other end of the pipe
    /// which is called the "shim"
    /// </summary>
    public class NetMQActor : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        /// <summary>
        /// The terminate-shim command.
        /// This is just the literal string "endPipe".
        /// </summary>
        public const string EndShimMessage = "endPipe";

        #region Action shim handlers

        private sealed class ActionShimHandler<T> : IShimHandler
        {
            private readonly ShimAction<T> m_action;
            private readonly T m_state;

            /// <summary>
            /// Create a new ActionShimHandler with the given type T to serve as the state-information,
            /// and the given action to operate upon that type.
            /// </summary>
            /// <param name="action">a ShimAction of type T that comprises the action to perform</param>
            /// <param name="state">the state-information</param>
            public ActionShimHandler([NotNull] ShimAction<T> action, T state)
            {
                m_action = action;
                m_state = state;
            }

            /// <summary>
            /// Perform the action upon the given shim, using our state-information.
            /// </summary>
            /// <param name="shim">a <see cref="PairSocket"/> that is the shim to perform the action upon</param>
            public void Run(PairSocket shim)
            {
                m_action(shim, m_state);
            }
        }

        private sealed class ActionShimHandler : IShimHandler
        {
            private readonly ShimAction m_action;

            /// <summary>
            /// Create a new ActionShimHandler with a given action to operate upon that type.
            /// </summary>
            /// <param name="action">a ShimAction that comprises the action to perform</param>
            public ActionShimHandler([NotNull] ShimAction action)
            {
                m_action = action;
            }

            /// <summary>
            /// Perform the action upon the given shim, using our state-information.
            /// </summary>
            /// <param name="shim">a <see cref="PairSocket"/> that is the shim to perform the action upon</param>
            public void Run(PairSocket shim)
            {
                m_action(shim);
            }
        }

        #endregion

        private readonly PairSocket m_self;
        private readonly PairSocket m_shim;

        private readonly Thread m_shimThread;
        private readonly IShimHandler m_shimHandler;

        private readonly EventDelegator<NetMQActorEventArgs> m_receiveEvent;
        private readonly EventDelegator<NetMQActorEventArgs> m_sendEvent;

        private int m_isDisposed;

        #region Creating Actor

        private NetMQActor(PairSocket self, PairSocket shim, [NotNull] IShimHandler shimHandler)
        {
            m_shimHandler = shimHandler;

            m_self = self;
            m_shim = shim;

            void OnReceive(object sender, NetMQSocketEventArgs e) => m_receiveEvent.Fire(this, new NetMQActorEventArgs(this));
            void OnSend   (object sender, NetMQSocketEventArgs e) => m_sendEvent   .Fire(this, new NetMQActorEventArgs(this));

            m_receiveEvent = new EventDelegator<NetMQActorEventArgs>(
                () => m_self.ReceiveReady += OnReceive,
                () => m_self.ReceiveReady -= OnReceive);

            m_sendEvent = new EventDelegator<NetMQActorEventArgs>(
                () => m_self.SendReady += OnSend,
                () => m_self.SendReady -= OnSend);

            var random = new Random();

            // Bind and connect pipe ends
            string actorName;
            string endPoint;
            while (true)
            {
                try
                {
                    actorName = $"NetMQActor-{random.Next(0, 10000)}-{random.Next(0, 10000)}";
                    endPoint = $"inproc://{actorName}";
                    m_self.Bind(endPoint);
                    break;
                }
                catch (AddressAlreadyInUseException)
                {
                    // Loop around and try another random address
                }
            }

            m_shim.Connect(endPoint);

            m_shimThread = new Thread(RunShim) { Name = actorName };
            m_shimThread.Start();

            // Mandatory handshake for new actor so that constructor returns only
            // when actor has also initialised. This eliminates timing issues at
            // application start up.
            m_self.ReceiveSignal();
        }

        /// <summary>
        /// Create a new <see cref="NetMQActor"/> with the given shimHandler.
        /// </summary>
        /// <param name="shimHandler">an <c>IShimHandler</c> that provides the Run method</param>
        /// <returns>the newly-created <c>NetMQActor</c></returns>
        [NotNull]
        public static NetMQActor Create([NotNull] IShimHandler shimHandler)
        {
            return new NetMQActor(new PairSocket(), new PairSocket(), shimHandler);
        }

        /// <summary>
        /// Create a new <see cref="NetMQActor"/> with the action, and state-information.
        /// </summary>
        /// <param name="action">a <c>ShimAction</c> - delegate for the action to perform</param>
        /// <param name="state">the state-information - of the generic type T</param>
        /// <returns>the newly-created <c>NetMQActor</c></returns>
        [NotNull]
        public static NetMQActor Create<T>([NotNull] ShimAction<T> action, T state)
        {
            return new NetMQActor(new PairSocket(), new PairSocket(), new ActionShimHandler<T>(action, state));
        }

        /// <summary>
        /// Create a new <see cref="NetMQActor"/> with the given <see cref="ShimAction"/>.
        /// </summary>
        /// <param name="action">a <c>ShimAction</c> - delegate for the action to perform</param>
        /// <returns>the newly-created <c>NetMQActor</c></returns>
        [NotNull]
        public static NetMQActor Create([NotNull] ShimAction action)
        {
            return new NetMQActor(new PairSocket(), new PairSocket(), new ActionShimHandler(action));
        }

        #endregion

        /// <summary>
        /// Execute the shim handler's Run method, signal ok and then dispose of the shim.
        /// </summary>
        private void RunShim()
        {
            try
            {
                m_shimHandler.Run(m_shim);
            }
            catch (TerminatingException)
            {
            }

            // Do not block, if the other end of the pipe is already deleted
            m_shim.TrySignalOK();

            m_shim.Dispose();
        }

        /// <summary>
        /// Transmit the given Msg over this actor's own socket.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to transmit</param>
        /// <param name="timeout">The maximum length of time to try and send a message. If <see cref="TimeSpan.Zero"/>, no
        /// wait occurs.</param>
        /// <param name="more">Indicate if another frame is expected after this frame</param>
        /// <returns><c>true</c> if a message was sent, otherwise <c>false</c>.</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        public bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            return m_self.TrySend(ref msg, timeout, more);
        }

        #region IReceivingSocket

        /// <summary>
        /// Attempt to receive a message for the specified period of time, returning true if successful or false if it times-out.
        /// </summary>
        /// <param name="msg">a <c>Msg</c> to write the received message into</param>
        /// <param name="timeout">a <c>TimeSpan</c> specifying how long to block, waiting for a message, before timing out</param>
        /// <returns>true only if a message was indeed received</returns>
        public bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            return m_self.TryReceive(ref msg, timeout);
        }

        #endregion

        #region Events Handling

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs> ReceiveReady
        {
            add => m_receiveEvent.Event += value;
            remove => m_receiveEvent.Event -= value;
        }

        /// <summary>
        /// This event occurs when a message is ready to be transmitted from the socket.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs> SendReady
        {
            add => m_sendEvent.Event += value;
            remove => m_sendEvent.Event -= value;
        }

        NetMQSocket ISocketPollable.Socket => m_self;

        #endregion

        #region Disposing

        /// <inheritdoc />
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
            if (Interlocked.CompareExchange(ref m_isDisposed, 1, 0) != 0)
                return;
            if (!disposing)
                return;

            // send destroy message to pipe
            if (m_self.TrySendFrame(EndShimMessage))
                m_self.ReceiveSignal();

            m_shimThread.Join();
            m_self.Dispose();
            m_sendEvent.Dispose();
            m_receiveEvent.Dispose();
        }

        /// <inheritdoc />
        public bool IsDisposed => m_isDisposed != 0;

        #endregion
    }
}
