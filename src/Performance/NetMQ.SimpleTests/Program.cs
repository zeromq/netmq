namespace NetMQ.SimpleTests
{
    using System;

    internal class Program
    {
        public static void Main(string[] args)
        {
            RunTests(
                new HelloWorld(),
                new LatencyBenchmark(),
                new ThroughputBenchmark());

            Console.WriteLine("Finished running tests.");
            Console.ReadLine();
        }

        private static void RunTests(params ITest[] tests)
        {
            foreach (ITest test in tests)
            {
                Console.WriteLine("Running test {0}...", test.TestName);
                Console.WriteLine();
                test.RunTest();
                Console.WriteLine();
            }
        }
    }
}
