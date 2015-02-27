using System;

namespace NetMQ.SimpleTests
{
    internal static class Program
    {
        public static void Main()
        {
            ITest[] tests =
            {
                new HelloWorld(),
                new LatencyBenchmark(),
                new ThroughputBenchmark()
            };

            foreach (var test in tests)
            {
                Console.WriteLine("Running test {0}...", test.TestName);
                Console.WriteLine();
                test.RunTest();
                Console.WriteLine();
            }

            Console.WriteLine("Finished running tests.");
            Console.ReadLine();
        }
    }
}