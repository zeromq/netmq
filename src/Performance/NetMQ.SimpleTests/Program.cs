using System;

namespace NetMQ.SimpleTests
{
    internal static class Program
    {
        public static void Main()
        {
            ITest[] tests =
            {
                new LatencyBenchmark(),
                new ThroughputBenchmark()
            };

            foreach (var test in tests)
            {
                Console.WriteLine("======== {0}", test.TestName);
                Console.WriteLine();
                test.RunTest();
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }
    }
}