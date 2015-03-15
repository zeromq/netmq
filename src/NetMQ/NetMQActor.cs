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

    public delegate void ShimAction(PairSocket shim);

    public delegate void ShimAction<in T>(PairSocket shim, T state);

    /// <summary>
    /// The Actor represents one end of a two-way pipe between 2 PairSocket(s). Where
    /// the actor may be passed messages, that are sent to the other end of the pipe
    /// which called the "shim"
    /// </summary>
    public class NetMQActor : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        /// <summary>
        /// This is just the literal string "endPipe".
        /// </summary>
        public const string EndShimMessage = "endPipe";

        #region Action shim handlers

        private class ActionShimHandler<T> : IShimHandler
        {
            private readonly ShimAction<T> m_action;
            private readonly T m_state;

            public ActionShimHandler(ShimAction<T> action, T state)
            {
                m_action = action;
                m_state = state;
            }

            public void Run([NotNull] PairSocket shim)
            {
                m_action(shim, m_state);
            }
        }

        private class ActionShimHandler : IShimHandler
        {
            private readonly ShimAction m_action;

            public ActionShimHandler(ShimAction action)
            {
                m_action = action;
            }

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

        private readonly EventDelegatorHelper<NetMQActorEventArgs> m_receiveEventDelegatorHelper;
        private readonly EventDelegatorHelper<NetMQActorEventArgs> m_sendEventDelegatorHelper;

        #region Creating Actor

        private NetMQActor([NotNull] NetMQContext context, [NotNull] IShimHandler shimHandler)
        {
            m_shimHandler = shimHandler;

            m_self = context.CreatePairSocket();
            m_shim = context.CreatePairSocket();

            m_receiveEventDelegatorHelper = new EventDelegatorHelper<NetMQActorEventArgs>(
                () => m_self.ReceiveReady += OnReceive,
                () => m_self.ReceiveReady += OnReceive);

            m_sendEventDelegatorHelper = new EventDelegatorHelper<NetMQActorEventArgs>(
                () => m_self.SendReady += OnReceive,
                () => m_self.SendReady += OnSend);

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

            //  Mandatory handshake for new actor so that constructor returns only
            //  when actor has also initialised. This eliminates timing issues at
            //  application start up.
            m_self.ReceiveSignal();
        }

        [NotNull]
        public static NetMQActor Create([NotNull] NetMQContext context, [NotNull] IShimHandler shimHandler)
        {
            return new NetMQActor(context, shimHandler);
        }

        [NotNull]
        public static NetMQActor Create<T>([NotNull] NetMQContext context, [NotNull] ShimAction<T> action, T state)
        {
            return new NetMQActor(context, new ActionShimHandler<T>(action, state));
        }

        [NotNull]
        public static NetMQActor Create([NotNull] NetMQContext context, [NotNull] ShimAction action)
        {
            return new NetMQActor(context, new ActionShimHandler(action));
        }

        #endregion

        private void RunShim()
        {
            try
            {
                m_shimHandler.Run(m_shim);
            }
            catch (TerminatingException)
            {}

            //  Do not block, if the other end of the pipe is already deleted
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
        /// Transmit the given Msg over this socket.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="options"></param>
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
            add { m_receiveEventDelegatorHelper.Event += value; }
            remove { m_receiveEventDelegatorHelper.Event -= value; }
        }

        /// <summary>
        /// This event occurs when a message is ready to be transmitted from the socket.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs> SendReady
        {
            add { m_sendEventDelegatorHelper.Event += value; }
            remove { m_sendEventDelegatorHelper.Event -= value; }
        }

        NetMQSocket ISocketPollable.Socket
        {
            get { return m_self; }
        }

        private void OnReceive(object sender, NetMQSocketEventArgs e)
        {
            m_receiveEventDelegatorHelper.Fire(this, new NetMQActorEventArgs(this));
        }

        private void OnSend(object sender, NetMQSocketEventArgs e)
        {
            m_sendEventDelegatorHelper.Fire(this, new NetMQActorEventArgs(this));
        }

        #endregion

        #region Disposing

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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
