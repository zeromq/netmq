using System;
using System.Net;
using System.Threading;
using NetMQ;

namespace Freelance.ModelOne.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket response = context.CreateResponseSocket())
                {
                    string address = GetComputerLanIP();
                    string port = "5555";

                    if (!string.IsNullOrEmpty(port))
                    {
                        if (!string.IsNullOrEmpty(address))
                        {
                            Console.WriteLine("Binding tcp://{0}:{1}", address, port);
                            response.Bind(string.Format("tcp://{0}:{1}", address, port));

                            while (true)
                            {
                                bool hasMore = true;
                                string msg = response.ReceiveString(out hasMore);
                                if (string.IsNullOrEmpty(msg))
                                {
                                    Console.WriteLine("No msg received.");
                                    break;
                                }

                                Console.WriteLine("Msg received! {0}", msg);
                                response.Send(msg, false, hasMore);

                                Thread.Sleep(1000);
                            }

                            response.Options.Linger = TimeSpan.Zero;
                        }
                        else
                        {
                            Console.WriteLine("Wrong IP address");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Please set NetMQPort variable into app.config file");
                    }

                    Console.WriteLine("Press ENTER to exit...");
                    Console.ReadLine();
                }
            }
        }

        private static string GetComputerLanIP()
        {
            string strHostName = Dns.GetHostName();

            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);

            foreach (IPAddress ipAddress in ipEntry.AddressList)
            {
                if (ipAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    return ipAddress.ToString();
                }
            }

            return "";
        }
    }
}
