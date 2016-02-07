using System;
using System.Threading;
using NetMQ;

namespace BeaconDemo
{
    /// <summary>
    /// Originally from http://netmq.readthedocs.org/en/latest/beacon/
    /// </summary>
    public static class Program
    {
        private static void Main()
        {
            Console.Title = "NetMQ Beacon Demo";

            // Create a bus using broadcast port 9999
            // All communication with the bus is through the returned actor
            var actor = Bus.Create(9999);

            actor.SendFrame(Bus.GetHostAddressCommand);
            var hostAddress = actor.ReceiveFrameString();

            Console.Title = string.Format("NetMQ Beacon Demo at {0}", hostAddress);

            // beacons publish every second, so wait a little longer than that to
            // let all the other nodes connect to our new node
            Thread.Sleep(1100);

            // publish a hello message
            // note we can use NetMQSocket send and receive extension methods
            actor.SendMoreFrame(Bus.PublishCommand).SendMoreFrame("Hello?").SendFrame(hostAddress);

            // receive messages from other nodes on the bus
            while (true)
            {
                // actor is receiving messages forwarded by the Bus subscriber
                string message = actor.ReceiveFrameString();
                switch (message)
                {
                    case "Hello?":
                        // another node is saying hello
                        var fromHostAddress = actor.ReceiveFrameString();
                        var msg = fromHostAddress + " says Hello?";
                        Console.WriteLine(msg);

                        // send back a welcome message via the Bus publisher
                        msg = hostAddress + " says Welcome!";
                        actor.SendMoreFrame(Bus.PublishCommand).SendFrame(msg);
                        break;
                    case Bus.AddedNodeCommand:
                        var addedAddress = actor.ReceiveFrameString();
                        Console.WriteLine("Added node {0} to the Bus", addedAddress);
                        break;
                    case Bus.RemovedNodeCommand:
                        var removedAddress = actor.ReceiveFrameString();
                        Console.WriteLine("Removed node {0} from the Bus", removedAddress);
                        break;
                    default:
                        // it's probably a welcome message
                        Console.WriteLine(message);
                        break;
                }
            }
        }
    }
}
