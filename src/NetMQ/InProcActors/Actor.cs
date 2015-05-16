using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetMQ.InProcActors;
using NetMQ.Sockets;

namespace NetMQ.Actors
{
    /// <summary>
    /// A NetMQActorEventArgs is an EventArgs that also provides an generically-typed Actor property.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete("Use non generic NetMQActor")]
    public class NetMQActorEventArgs<T> : EventArgs
    {
        /// <summary>
        /// Create a new NetMQActorEventArgs with the given actor.
        /// </summary>
        /// <param name="actor">the Actor for the new NetMQActorEventArgs to contain</param>
        public NetMQActorEventArgs([NotNull] Actor<T> actor)
        {
            Actor = actor;
        }

        /// <summary>
        /// Get the Actor object that this NetMQActorEventArgs contains.
        /// </summary>
        [NotNull]
        public Actor<T> Actor { get; private set; }
    }

    /// <summary>
    /// The Actor represents one end of a two way pipe between 2 PairSocket(s). Where
    /// the actor may be passed messages, that are sent to the other end of the pipe
    /// which I am calling the "shim"
    /// </summary>
    [Obsolete("Use non generic NetMQActor")]
    public class Actor<T> : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        private static readonly Random s_rand = new Random();

        private readonly PairSocket m_self;
        private readonly Shim<T> m_shim;
        private Task m_shimTask;
        private readonly EventDelegator<NetMQActorEventArgs<T>> m_receiveEvent;
        private readonly EventDelegator<NetMQActorEventArgs<T>> m_sendEvent;

        [NotNull]
        private static string GetEndPointName()
        {
            return string.Format("inproc://zactor-{0}-{1}",
                s_rand.Next(0, 10000), s_rand.Next(0, 10000));
        }

        /// <summary>
        /// Create a new Actor within the given shim-handler and state-information.
        /// </summary>
        /// <param name="context">the context for this actor to live within</param>
        /// <param name="shimHandler">this <see cref="IShimHandler"/> is will handle the actions we send to the shim</param>
        /// <param name="state">this generic type represents the state-information that the action will use</param>
        public Actor([NotNull] NetMQContext context, [NotNull] IShimHandler<T> shimHandler, T state)
        {
            m_self = context.CreatePairSocket();
            m_shim = new Shim<T>(shimHandler, context.CreatePairSocket());
            m_self.Options.SendHighWatermark = 1000;
            m_self.Options.SendHighWatermark = 1000;

            EventHandler<NetMQSocketEventArgs> onReceive = (sender, e) =>
                m_receiveEvent.Fire(this, new NetMQActorEventArgs<T>(this));

            EventHandler<NetMQSocketEventArgs> onSend = (sender, e) =>
                m_sendEvent.Fire(this, new NetMQActorEventArgs<T>(this));

            m_receiveEvent = new EventDelegator<NetMQActorEventArgs<T>>(
                () => m_self.ReceiveReady += onReceive,
                () => m_self.ReceiveReady -= onReceive);

            m_sendEvent = new EventDelegator<NetMQActorEventArgs<T>>(
                () => m_self.SendReady += onSend,
                () => m_self.SendReady -= onSend);

            //now binding and connect pipe ends
            string endPoint = string.Empty;
            while (true)
            {
                Action bindAction = () =>
                {
                    endPoint = GetEndPointName();
                    m_self.Bind(endPoint);
                };

                try
                {
                    bindAction();
                    break;
                }
                catch (NetMQException nex)
                {
                    if (nex.ErrorCode == ErrorCode.EFAULT)
                    {
                        bindAction();
                    }
                }
            }

            m_shim.Pipe.Connect(endPoint);

            try
            {
                //Initialise the shim handler
                m_shim.Handler.Initialise(state);
            }
            catch (Exception)
            {
                m_self.Dispose();
                m_shim.Pipe.Dispose();

                throw;
            }

            //Create Shim thread handler
            CreateShimThread();

            // Mandatory handshake for new actor so that constructor returns only
            // when actor has also initialized. This eliminates timing issues at
            // application start up.
            m_self.WaitForSignal();
        }

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs<T>> ReceiveReady
        {
            add { m_receiveEvent.Event += value; }
            remove { m_receiveEvent.Event -= value; }
        }

        /// <summary>
        /// This event occurs when a message is ready to be transmitted from the socket.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs<T>> SendReady
        {
            add { m_sendEvent.Event += value; }
            remove { m_sendEvent.Event -= value; }
        }

        NetMQSocket ISocketPollable.Socket
        {
            get { return m_self; }
        }

        private void CreateShimThread()
        {
            // start shim task
            m_shimTask = Task.Factory.StartNew(
                x =>
                {
                    try
                    {
                        m_shim.Handler.RunPipeline(m_shim.Pipe);
                    }
                    catch (TerminatingException)
                    {
                    }

                    // Do not block, if the other end of the pipe is already deleted
                    m_shim.Pipe.Options.SendTimeout = TimeSpan.Zero;

                    try
                    {
                        m_shim.Pipe.SignalOK();
                    }
                    catch (AgainException)
                    {
                    }

                    m_shim.Pipe.Dispose();
                },
                TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        public void Dispose()
        {
            //send destroy message to pipe
            m_self.Options.SendTimeout = TimeSpan.Zero;
            try
            {
                m_self.Send(ActorKnownMessages.END_PIPE);
                m_self.WaitForSignal();
            }
            catch (AgainException)
            {
            }

            m_shimTask.Wait();
            m_self.Dispose();
            m_sendEvent.Dispose();
            m_receiveEvent.Dispose();
        }

        #region IOutgoingSocket

        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        /// <exception cref="AgainException">The send operation timed out.</exception>
        [Obsolete("Use Send(ref Msg,bool) or TrySend(ref Msg,TimeSpan,bool) instead.")]
        public void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Send(ref msg, options);
        }

        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        public bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            return m_self.TrySend(ref msg, timeout, more);
        }

        #endregion

        #region IReceivingSocket

        /// <exception cref="AgainException">The receive operation timed out.</exception>
        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Receive(ref msg, options);
        }

        /// <summary>
        /// Attempt to receive a message for the specified amount of time.
        /// </summary>
        /// <param name="msg">A reference to a <see cref="Msg"/> instance into which the received message data should be placed.</param>
        /// <param name="timeout">The maximum amount of time the call should wait for a message before returning.</param>
        /// <returns><c>true</c> if a message was received before <paramref name="timeout"/> elapsed,
        /// otherwise <c>false</c>.</returns>
        public bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            return m_self.TryReceive(ref msg, timeout);
        }

        #endregion
    }
}