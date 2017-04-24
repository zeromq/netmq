using System;
using NetMQ.Sockets;

namespace NetMQ.Tests.InProcActors.Echo
{
    /// <summary>
    /// This handler class illustrates a specific implementation that you would need
    /// to implement per actor. This essentially contains your commands/protocol
    /// and should deal with any command workload, as well as sending back to the
    /// other end of the PairSocket which calling code would receive by using the
    /// Actor classes' various ReceiveXXX() methods
    ///
    /// This is a VERY simple protocol. It just demonstrates what you would need
    /// to do to implement your own Shim handler.
    ///
    /// The only things you MUST do are
    ///
    /// 1. Bad commands should always send the following message
    ///    "Error: invalid message to actor"
    /// 2. When we receive a command from the actor telling us to exit the pipeline we should immediately
    ///    break out of the while loop, and dispose of the shim socket.
    /// 3. When an Exception occurs you should send that down the wire to Actors' calling code.
    /// </summary>
    public class EchoShimHandler : IShimHandler
    {
        public void Run(PairSocket shim)
        {
            shim.SignalOK();

            while (true)
            {
                try
                {
                    //Message for this actor/shim handler is expected to be
                    //Frame[0] : Command
                    //Frame[1] : Payload
                    //
                    //Result back to actor is a simple echoing of the Payload, where
                    //the payload is prefixed with "ECHO BACK "
                    NetMQMessage msg = shim.ReceiveMultipartMessage();

                    string command = msg[0].ConvertToString();

                    switch (command)
                    {
                        case NetMQActor.EndShimMessage:
                            return;
                        case "ECHO":
                            shim.SendFrame($"ECHO BACK : {msg[1].ConvertToString()}");
                            break;
                        default:
                            shim.SendFrame("Error: invalid message to actor");
                            break;
                    }
                }
                // You WILL need to decide what Exceptions should be caught here, this is for
                // demonstration purposes only, any unhandled fault will bubble up to caller's code
                catch (Exception e)
                {
                    shim.SendFrame($"Error: Exception occurred {e.Message}");
                }
            }
        }
    }
}
