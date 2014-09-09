using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Sockets;
using NetMQ.zmq;
using NetMQ.Actors;

namespace NetMQ.Tests.InProcActors.Echo
{
    /// <summary>
    /// This hander class is specific implementation that you would need
    /// to implement per actor. This essentially contains your commands/protocol
    /// and should deal with any command workload, as well as sending back to the
    /// other end of the PairSocket which calling code would receive by using the
    /// Actor classes various RecieveXXX() methods
    /// 
    /// This is a VERY simple protocol but it just demonstrates what you would need
    /// to do to implement your own Shim handler
    /// </summary>
    public class EchoShimHandler : IShimHandler, IDisposable
    {

        private bool shouldRun = true;
        private bool shouldRaiseException=false;


        public void Run(PairSocket shim, object[] args, CancellationToken token)
        {
            if (args == null || args.Count() != 1 || (string)args[0] != "Hello World")
                throw new InvalidOperationException(
                    "Args were not correct, expected 'Hello World'");

            while (shouldRun)
            {
               token.ThrowIfCancellationRequested();

                //Message for this actor/shim handler is expected to be 
                //Frame[0] : Command
                //Frame[1] : Payload
                //
                //Result back to actor is a simple echoing of the Payload, where
                //the payload is prefixed with "ECHO BACK "
                NetMQMessage msg = null;

                //this may throw NetMQException if we have disposed of the actor
                //end of the pipe, and the CancellationToken.IsCancellationRequested 
                //did not get picked up this loop cycle
                msg = shim.ReceiveMessage();


                if (msg == null)
                    break;

                if (msg[0].ConvertToString() == "ECHO")
                {
                    shim.Send(string.Format("ECHO BACK : {0}",
                        msg[1].ConvertToString()));
                }
                else
                {
                    throw NetMQException.Create("Unexpected command",
                        ErrorCode.EFAULT);
                }

            }
        }

        public void Dispose()
        {
            shouldRun = false;
        }
    }
}
