using System;
using NetMQ;
using NetMQ.Sockets;

namespace WeatherUpdateServer
{
    internal static class WeatherUpdateServer
    {
        private static void Main()
        {
            Console.Title = "NetMQ Weather Update Server";

            bool stopRequested = false;

            // Wire up the CTRL+C handler
            Console.CancelKeyPress += (sender, e) => stopRequested = true;

            Console.WriteLine("Publishing weather updates...");

            using (var publisher = new PublisherSocket())
            {
                publisher.Bind("tcp://127.0.0.1:5556");

                var rng = new Random();

                while (!stopRequested)
                {
                    int zipcode = rng.Next(0, 99999);
                    int temperature = rng.Next(-80, 135);
                    int relhumidity = rng.Next(0, 90);

                    publisher.SendFrame(string.Format("{0} {1} {2}", zipcode, temperature, relhumidity));
                }
            }
        }
    }
}
