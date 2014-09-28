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
    /// <summary>
    /// The Actor represents one end of a two way pipe between 2 PairSocket(s). Where
    /// the actor may be passed messages, that are sent to the other end of the pipe
    /// which I am calling the "shim"
    /// </summary>
    public class Actor<T> : IOutgoingSocket, IReceivingSocket, IDisposable
    {
        private readonly PairSocket m_self;
        private readonly Shim<T> m_shim;
        private readonly Random rand = new Random();
        private T m_state;

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

        private void CreateShimThread(T state)
        {
            //start Shim thread
            Task shimTask = Task.Factory.StartNew(
                x => this.m_shim.Handler.RunPipeline(this.m_shim.Pipe),
                TaskCreationOptions.LongRunning);

            //pipeline dead if task completed, no matter what state it completed in
            //it is unusable to the Actors socket, to dispose of Actors socket
            shimTask.ContinueWith(antecedant =>
            {
                //Dispose of own socket
                if (m_self != null) m_self.Dispose();

            });
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
                m_self.Send(ActorKnownMessages.END_PIPE);
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
