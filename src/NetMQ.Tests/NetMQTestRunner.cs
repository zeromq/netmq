using System;
using System.Reflection;

namespace NetMQ.Tests
{
    class NetMQTestRunner
    {
        [STAThread]
        static void Main(string[] args)
        {
            NUnit.ConsoleRunner.Runner.Main(new string[] { Assembly.GetExecutingAssembly().Location });
        }
    }
}
