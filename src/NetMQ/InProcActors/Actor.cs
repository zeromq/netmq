using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    public class Actor : IOutgoingSocket, IReceivingSocket, IDisposable
    {
        private readonly Action<Exception> pipeExceptionCallback;
        private readonly PairSocket self;
        private readonly Shim shim;
        private Random rand = new Random();
        private CancellationTokenSource cts = new CancellationTokenSource();

        private string GetEndPointName()
        {
            return string.Format("inproc://zactor-{0}-{1}",
                rand.Next(0, 10000), rand.Next(0, 10000));
        }

        public Actor(NetMQContext context, Action<Exception> pipeExceptionCallback, 
            IShimHandler shimHandler, object[] args)
        {
            this.pipeExceptionCallback = pipeExceptionCallback;
            this.self = context.CreatePairSocket();
            this.shim = new Shim(shimHandler, context.CreatePairSocket());
            this.self.Options.SendHighWatermark = 1000;
            this.self.Options.SendHighWatermark = 1000;

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

            //Create Shim thread handler
            CreateShimThread(args);
        }

        private void CreateShimThread(object[] args)
        {
            Task shimTask = Task.Factory.StartNew(
                (state) => this.shim.Handler.Run(this.shim.Pipe, (object[])state, cts.Token),
                args,
                cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);


            shimTask.ContinueWith(ant =>
            {
                if (ant.Exception == null) return;
                if (pipeExceptionCallback == null) return;

                var ex = ant.Exception.Flatten().GetBaseException();
                pipeExceptionCallback(ex);
            }, TaskContinuationOptions.OnlyOnFaulted);
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

        public void Cancel()
        {
            //cancel shim thread
            cts.Cancel();
        }

        protected virtual void Dispose(bool disposing)
        {

            // release other disposable objects
            if (disposing)
            {
                if (self != null) self.Dispose();
                if (shim != null) shim.Dispose();
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
