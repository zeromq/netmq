using System;
using System.Threading;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ
{
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
        public NetMQActor Actor { get; private set; }
    }

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

    /// <summary>
    /// The Actor represents one end of a two-way pipe between 2 PairSocket(s). Where
    /// the actor may be passed messages, that are sent to the other end of the pipe
    /// which called the "shim"
    /// </summary>
    public class NetMQActor : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        /// <summary>
        /// The terminate-shim command.
        /// This is just the literal string "endPipe".
        /// </summary>
        public const string EndShimMessage = "endPipe";

        #region Action shim handlers

        private class ActionShimHandler<T> : IShimHandler
        {
            private readonly ShimAction<T> m_action;
            private readonly T m_state;

            /// <summary>
            /// Create a new ActionShimHandler with the given type T to serve as the state-information,
            /// and the given action to operate upon that type.
            /// </summary>
            /// <param name="action">a ShimAction of type T that comprises the action to perform</param>
            /// <param name="state">the state-information</param>
            public ActionShimHandler(ShimAction<T> action, T state)
            {
                m_action = action;
                m_state = state;
            }

            /// <summary>
            /// Perform the action upon the given shim, using our state-information.
            /// </summary>
            /// <param name="shim">a <see cref="PairSocket"/> that is the shim to perform the action upon</param>
            public void Run([NotNull] PairSocket shim)
            {
                m_action(shim, m_state);
            }
        }

        private class ActionShimHandler : IShimHandler
        {
            private readonly ShimAction m_action;

            /// <summary>
            /// Create a new ActionShimHandler with a given action to operate upon that type.
            /// </summary>
            /// <param name="action">a ShimAction that comprises the action to perform</param>
            public ActionShimHandler(ShimAction action)
            {
                m_action = action;
            }

            /// <summary>
            /// Perform the action upon the given shim, using our state-information.
            /// </summary>
            /// <param name="shim">a <see cref="PairSocket"/> that is the shim to perform the action upon</param>
            public void Run([NotNull] PairSocket shim)
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

        #region Creating Actor

        private NetMQActor([NotNull] NetMQContext context, [NotNull] IShimHandler shimHandler)
        {
            m_shimHandler = shimHandler;

            m_self = context.CreatePairSocket();
            m_shim = context.CreatePairSocket();

            m_receiveEvent = new EventDelegator<NetMQActorEventArgs>(
                () => m_self.ReceiveReady += OnReceive,
                () => m_self.ReceiveReady -= OnReceive);

            m_sendEvent = new EventDelegator<NetMQActorEventArgs>(
                () => m_self.SendReady += OnSend,
                () => m_self.SendReady -= OnSend);

            var random = new Random();

            //now binding and connect pipe ends
            string endPoint;
            while (true)
            {
                try
                {
                    endPoint = string.Format("inproc://NetMQActor-{0}-{1}", random.Next(0, 10000), random.Next(0, 10000));
                    m_self.Bind(endPoint);
                    break;
                }
                catch (AddressAlreadyInUseException)
                {
                    // In case address already in use we continue searching for an address
                }
            }

            m_shim.Connect(endPoint);

            m_shimThread = new Thread(RunShim);
            m_shimThread.Start();

            // Mandatory handshake for new actor so that constructor returns only
            // when actor has also initialised. This eliminates timing issues at
            // application start up.
            m_self.ReceiveSignal();
        }

        /// <summary>
        /// Create a new <c>NetMQActor</c> with the given context and shimHandler.
        /// </summary>
        /// <param name="context">the context for this actor to live within</param>
        /// <param name="shimHandler">an <c>IShimHandler</c> that provides the Run method</param>
        /// <returns>the newly-created <c>NetMQActor</c></returns>
        [NotNull]
        public static NetMQActor Create([NotNull] NetMQContext context, [NotNull] IShimHandler shimHandler)
        {
            return new NetMQActor(context, shimHandler);
        }

        /// <summary>
        /// Create a new <c>NetMQActor</c> with the given context, action, and state-information.
        /// </summary>
        /// <param name="context">the context for this actor to live within</param>
        /// <param name="action">a <c>ShimAction</c> - delegate for the action to perfrom</param>
        /// <param name="state">the state-information - of the generic type T</param>
        /// <returns>the newly-created <c>NetMQActor</c></returns>
        [NotNull]
        public static NetMQActor Create<T>([NotNull] NetMQContext context, [NotNull] ShimAction<T> action, T state)
        {
            return new NetMQActor(context, new ActionShimHandler<T>(action, state));
        }

        /// <summary>
        /// Create a new <c>NetMQActor</c> with the given context and <see cref="ShimAction"/>.
        /// </summary>
        /// <param name="context">the context for this actor to live within</param>
        /// <param name="action">a <c>ShimAction</c> - delegate for the action to perform</param>
        /// <returns>the newly-created <c>NetMQActor</c></returns>
        [NotNull]
        public static NetMQActor Create([NotNull] NetMQContext context, [NotNull] ShimAction action)
        {
            return new NetMQActor(context, new ActionShimHandler(action));
        }

        #endregion

        /// <summary>
        /// Execute the shimhandler's Run method, signal ok and then dispose of the shim.
        /// </summary>
        private void RunShim()
        {
            try
            {
                m_shimHandler.Run(m_shim);
            }
            catch (TerminatingException)
            {}

            // Do not block, if the other end of the pipe is already deleted
            m_shim.Options.SendTimeout = TimeSpan.Zero;

            try
            {
                m_shim.SignalOK();
            }
            catch (AgainException)
            {}

            m_shim.Dispose();
        }

        /// <summary>
        /// Transmit the given Msg over this actor's own socket.
        /// </summary>
        /// <param name="msg">the <c>Msg</c> to transmit</param>
        /// <param name="options">this denotes any of the DontWait and SendMore flags be set</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        /// <exception cref="AgainException">The send operation timed out.</exception>
        public void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Send(ref msg, options);
        }

        #region IReceivingSocket

        /// <exception cref="AgainException">The receive operation timed out.</exception>
        [Obsolete("Use Receive(ref Msg) or TryReceive(ref Msg,TimeSpan) instead.")]
        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Receive(ref msg, options);
        }

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
            add { m_receiveEvent.Event += value; }
            remove { m_receiveEvent.Event -= value; }
        }

        /// <summary>
        /// This event occurs when a message is ready to be transmitted from the socket.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs> SendReady
        {
            add { m_sendEvent.Event += value; }
            remove { m_sendEvent.Event -= value; }
        }

        NetMQSocket ISocketPollable.Socket
        {
            get { return m_self; }
        }

        private void OnReceive(object sender, NetMQSocketEventArgs e)
        {
            m_receiveEvent.Fire(this, new NetMQActorEventArgs(this));
        }

        private void OnSend(object sender, NetMQSocketEventArgs e)
        {
            m_sendEvent.Fire(this, new NetMQActorEventArgs(this));
        }

        #endregion

        #region Disposing

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

            // send destroy message to pipe
            m_self.Options.SendTimeout = TimeSpan.Zero;
            try
            {
                m_self.Send(EndShimMessage);
                m_self.ReceiveSignal();
            }
            catch (AgainException)
            {}

            m_shimThread.Join();
            m_self.Dispose();
        }

        #endregion
    }
}
