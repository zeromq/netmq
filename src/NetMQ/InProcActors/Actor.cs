using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetMQ.InProcActors;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ.Actors
{
    /// <summary>
    /// A NetMQActorEventArgs is an EventArgs that also provides an generically-typed Actor property.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete("Use non generic NetMQActor")]
    public class NetMQActorEventArgs<T> : EventArgs
    {
        public NetMQActorEventArgs([NotNull] Actor<T> actor)
        {
            Actor = actor;
        }

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
        private readonly EventDelegatorHelper<NetMQActorEventArgs<T>> m_receiveEventDelegatorHelper;
        private readonly EventDelegatorHelper<NetMQActorEventArgs<T>> m_sendEventDelegatorHelper;

        [NotNull]
        private static string GetEndPointName()
        {
            return string.Format("inproc://zactor-{0}-{1}",
                s_rand.Next(0, 10000), s_rand.Next(0, 10000));
        }

        public Actor([NotNull] NetMQContext context, [NotNull] IShimHandler<T> shimHandler, T state)
        {
            m_self = context.CreatePairSocket();
            m_shim = new Shim<T>(shimHandler, context.CreatePairSocket());
            m_self.Options.SendHighWatermark = 1000;
            m_self.Options.SendHighWatermark = 1000;

            m_receiveEventDelegatorHelper = new EventDelegatorHelper<NetMQActorEventArgs<T>>(
                () => m_self.ReceiveReady += OnReceive,
                () => m_self.ReceiveReady += OnReceive);
            m_sendEventDelegatorHelper = new EventDelegatorHelper<NetMQActorEventArgs<T>>(
                () => m_self.SendReady += OnReceive,
                () => m_self.SendReady += OnSend);

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

            //  Mandatory handshake for new actor so that constructor returns only
            //  when actor has also initialized. This eliminates timing issues at
            //  application start up.
            m_self.WaitForSignal();
        }

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs<T>> ReceiveReady
        {
            add { m_receiveEventDelegatorHelper.Event += value; }
            remove { m_receiveEventDelegatorHelper.Event -= value; }
        }

        /// <summary>
        /// This event occurs when a message is ready to be transmitted from the socket.
        /// </summary>
        public event EventHandler<NetMQActorEventArgs<T>> SendReady
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
            m_receiveEventDelegatorHelper.Fire(this, new NetMQActorEventArgs<T>(this));
        }

        private void OnSend(object sender, NetMQSocketEventArgs e)
        {
            m_sendEventDelegatorHelper.Fire(this, new NetMQActorEventArgs<T>(this));
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
                    {}

                    //  Do not block, if the other end of the pipe is already deleted
                    m_shim.Pipe.Options.SendTimeout = TimeSpan.Zero;

                    try
                    {
                        m_shim.Pipe.SignalOK();
                    }
                    catch (AgainException)
                    {}

                    m_shim.Pipe.Dispose();
                },
                TaskCreationOptions.LongRunning);
        }

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
            {}

            m_shimTask.Wait();
            m_self.Dispose();
        }

        #region IOutgoingSocket

        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        /// <exception cref="AgainException">The send operation timed out.</exception>
        public void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Send(ref msg, options);
        }

        #endregion

        #region IReceivingSocket

        /// <exception cref="AgainException">The receive operation timed out.</exception>
        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Receive(ref msg, options);
        }

        public bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            return m_self.TryReceive(ref msg, timeout);
        }

        #endregion
    }
}