using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace BeaconDemo
{
    /// <summary>
    /// From http://netmq.readthedocs.org/en/latest/beacon/
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "NetMQ Beacon Demo";

            // Create a bus using broadcast port 9999
            // All communication with the bus is through the returned actor
            var actor = Bus.Create(9999);

            actor.SendFrame(Bus.GetHostNameCommand);
            var hostName = actor.ReceiveFrameString();

            Console.Title = string.Format("NetMQ Beacon Demo - Beacon from {0}:{1}", hostName, "???");

            // beacons publish every second, so wait a little longer than that to
            // let all the other nodes connect to our new node
            Thread.Sleep(1100);

            // publish a hello message
            // note we can use NetMQSocket send and receive extension methods
            actor.SendMoreFrame(Bus.PublishCommand).SendFrame("Hello?");

            // receive messages from other nodes on the bus
            while (true)
            {
                string message = actor.ReceiveFrameString();

                if (message == "Hello?")
                {
                    // another node is saying hello
                    Console.WriteLine(message);

                    // send back a welcome message
                    actor.SendMoreFrame(Bus.PublishCommand).SendFrame("Welcome!");
                }
                else
                {
                    // it's probably a welcome message
                    Console.WriteLine(message);
                }
            }
        }
    }
}
