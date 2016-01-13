using System;
using System.Globalization;
using NetMQ;
using NetMQ.Sockets;

namespace WeatherUpdateClient
{
    internal static class WeatherUpdateClient
    {
        private static void Main()
        {
            Console.Title = "NetMQ Weather Update Client";

            const int zipToSubscribeTo = 10001;
            const int iterations = 100;

            int totalTemp = 0;
            int totalHumidity = 0;

            Console.WriteLine("Collecting updates for weather service for zipcode {0}...", zipToSubscribeTo);

            using (var subscriber = new SubscriberSocket())
            {
                subscriber.Connect("tcp://127.0.0.1:5556");
                subscriber.Subscribe(zipToSubscribeTo.ToString(CultureInfo.InvariantCulture));

                for (int i = 0; i < iterations; i++)
                {
                    string results = subscriber.ReceiveFrameString();
                    Console.Write(".");

                    // "zip temp relh" ... "10001 84 23" -> ["10001", "84", "23"]
                    string[] split = results.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    int zip = int.Parse(split[0]);
                    if (zip != zipToSubscribeTo)
                    {
                        throw new Exception(string.Format("Received message for unexpected zipcode: {0} (expected {1})", zip, zipToSubscribeTo));
                    }

                    totalTemp += int.Parse(split[1]);
                    totalHumidity += int.Parse(split[2]);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Average temperature was: {0}", totalTemp/iterations);
            Console.WriteLine("Average relative humidity was: {0}", totalHumidity/iterations);
        }
    }
}