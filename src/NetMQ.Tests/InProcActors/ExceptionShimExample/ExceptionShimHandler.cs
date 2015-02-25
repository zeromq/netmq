using System;
using NetMQ.InProcActors;
using NetMQ.Sockets;

namespace NetMQ.Tests.InProcActors.ExceptionShimExample
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
    /// 
    /// The only things you MUST do is to follow this example for handling
    /// a fews things
    /// 
    /// 1. Bad commands should always send the following message
    ///    "Error: invalid message to actor"
    /// 2. When we recieve a command from the actor telling us to exit the pipeline we should immediately
    ///    break out of the while loop, and dispose of the shim socket
    /// 3. When an Exception occurs you should send that down the wire to Actors calling code
    /// </summary>
    public class ExceptionShimHandler : IShimHandler<object>
    {

        public void Initialise(object state)
        {

        }


        public void RunPipeline(PairSocket shim)
        {
            shim.SignalOK();

            while (true)
            {
                try
                {
                    NetMQMessage msg = shim.ReceiveMessage();

                    string command = msg[0].ConvertToString();

                    if (command == NetMQActor.EndShimMessage)
                        break;

                    //Simulate a failure that should be sent back to Actor                    
                    throw new InvalidOperationException("Actors Shim threw an Exception");
                }
                //You WILL need to decide what Exceptions should be caught here, this is for 
                //demonstration purposes only, any unhandled falut will bubble up to callers code
                catch (InvalidOperationException e)
                {
                    shim.Send(string.Format("Error: Exception occurred {0}", e.Message));
                }
            }
        }
    }
}
