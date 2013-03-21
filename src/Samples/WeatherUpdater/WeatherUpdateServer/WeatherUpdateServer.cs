using System;
using System.Threading;
using NetMQ;

namespace WeatherUpdateServer
{
    internal class WeatherUpdateServer
    {
        readonly static ManualResetEvent _terminateEvent = new ManualResetEvent(false);

        private static void Main(string[] args)
        {
            // wire up the CTRL+C handler
            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("Publishing weather updates...");

            using (var context = NetMQContext.Create())
            using (var publisher = context.CreatePublisherSocket())
            {
                publisher.Bind("tcp://127.0.0.1:5556");

                var rng = new Random();

                while (_terminateEvent.WaitOne(0) == false)
                {
                    int zipcode = rng.Next(0, 99999);
                    int temperature = rng.Next(-80, 135);
                    int relhumidity = rng.Next(0, 90);
 
                    publisher.Send(string.Format("{0} {1} {2}", zipcode, temperature, relhumidity));
                }
            }
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _terminateEvent.Set();
        }
    }
}
