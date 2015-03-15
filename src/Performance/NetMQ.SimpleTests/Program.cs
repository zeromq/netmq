using System;

namespace NetMQ.SimpleTests
{
    internal static class Program
    {
        public static void Main()
        {
            // TODO include inproc variations
            // TODO print system specs, as possible

            ITest[] tests =
            {
                new LatencyBenchmark(),
                new LatencyBenchmarkReusingMsg(),
                new ThroughputBenchmark(),
                new ThroughputBenchmarkReusingMsg()
            };

            Console.WriteLine();

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