using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.InProcActors;
using NetMQ.Sockets;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.Actors
{
    public class NetMQActorEventArgs<T> : EventArgs
    {
        public NetMQActorEventArgs(Actor<T> actor)
        {
            Actor = actor;
        }

        public Actor<T> Actor { get; private set; }
    }

    /// <summary>
    /// The Actor represents one end of a two way pipe between 2 PairSocket(s). Where
    /// the actor may be passed messages, that are sent to the other end of the pipe
    /// which I am calling the "shim"
    /// </summary>
    public class Actor<T> : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        private readonly PairSocket m_self;
        private readonly Shim<T> m_shim;
        private readonly Random rand = new Random();
        private T m_state;

        private EventDelegatorHelper<NetMQActorEventArgs<T>> m_receiveEventDelegatorHelper;
        private EventDelegatorHelper<NetMQActorEventArgs<T>> m_sendEventDelegatorHelper; 

        private string GetEndPointName()
        {
            return string.Format("inproc://zactor-{0}-{1}",
                rand.Next(0, 10000), rand.Next(0, 10000));
        }

        public Actor(NetMQContext context,
            IShimHandler<T> shimHandler, T state)
        {
            this.m_self = context.CreatePairSocket();
            this.m_shim = new Shim<T>(shimHandler, context.CreatePairSocket());
            this.m_self.Options.SendHighWatermark = 1000;
            this.m_self.Options.SendHighWatermark = 1000;
            this.m_state = state;

            m_receiveEventDelegatorHelper = new EventDelegatorHelper<NetMQActorEventArgs<T>>(() => m_self.ReceiveReady += OnReceive,
                () => m_self.ReceiveReady += OnReceive);
            m_sendEventDelegatorHelper = new EventDelegatorHelper<NetMQActorEventArgs<T>>(() => m_self.SendReady += OnReceive,
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

            //Initialise the shim handler
            this.m_shim.Handler.Initialise(state);

            //Create Shim thread handler
            CreateShimThread(state);

            //  Mandatory handshake for new actor so that constructor returns only
            //  when actor has also initialized. This eliminates timing issues at
            //  application start up.
            m_self.WaitForSignal();
        }

      

        public event EventHandler<NetMQActorEventArgs<T>> ReceiveReady
        {
            add { m_receiveEventDelegatorHelper.Event += value; }
            remove { m_receiveEventDelegatorHelper.Event -= value; }
        }

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

        private void CreateShimThread(T state)
        {
            //start Shim thread
            Task shimTask = Task.Factory.StartNew(
                x =>
                {
                    try
                    {
                        this.m_shim.Handler.RunPipeline(this.m_shim.Pipe);
                    }
                    catch (TerminatingException)
                    {

                    }          
                    catch (Exception)
                    {
                        throw;
                    }

                    //  Do not block, if the other end of the pipe is already deleted
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


        ~Actor()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // release other disposable objects
            if (disposing)
            {
                //send destroy message to pipe
                m_self.Options.SendTimeout = TimeSpan.Zero;
                try
                {
                    m_self.Send(ActorKnownMessages.END_PIPE);
                    m_self.WaitForSignal();
                }
                catch (AgainException ex)
                {
                                        
                }                                

                m_self.Dispose();
            }
        }

        public void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Send(ref msg, options);
        }

        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            m_self.Receive(ref msg, options);
        }
    }
}
