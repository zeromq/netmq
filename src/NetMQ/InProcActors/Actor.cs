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
        private readonly PairSocket self;
        private readonly Shim<T> shim;
        private Random rand = new Random();
        private T state;

        private string GetEndPointName()
        {
            return string.Format("inproc://zactor-{0}-{1}",
                rand.Next(0, 10000), rand.Next(0, 10000));
        }

        public Actor(NetMQContext context,
            IShimHandler<T> shimHandler, T state)
        {
            this.self = context.CreatePairSocket();
            this.shim = new Shim<T>(shimHandler, context.CreatePairSocket());
            this.self.Options.SendHighWatermark = 1000;
            this.self.Options.SendHighWatermark = 1000;
            this.state = state;

            //now binding and connect pipe ends
            string endPoint = string.Empty;
            while (true)
            {
                Action bindAction = () =>
                {
                    endPoint = GetEndPointName();
                    self.Bind(endPoint);
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

            shim.Pipe.Connect(endPoint);

            //Initialise the shim handler
            this.shim.Handler.Initialise(state);

            //Create Shim thread handler
            CreateShimThread(state);
        }


       


        private void CreateShimThread(T state)
        {
            //start Shim thread
            Task shimTask = Task.Factory.StartNew(
                x => this.shim.Handler.RunPipeline(this.shim.Pipe),
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
                self.Send(ActorKnownMessages.END_PIPE);

                //Dispose of own socket
                if (self != null) self.Dispose();
            }
        }

        public void Send(ref Msg msg, SendReceiveOptions options)
        {
            self.Send(ref msg, options);
        }

        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            self.Receive(ref msg, options);
        }
    }
}
