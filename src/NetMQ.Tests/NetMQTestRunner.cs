using System;
using System.Reflection;
using NUnit.ConsoleRunner;

namespace NetMQ.Tests
{
    internal static class NetMQTestRunner
    {
        [STAThread]
        private static void Main()
        {
            Runner.Main(new[] { Assembly.GetExecutingAssembly().Location });
        }
    }
}