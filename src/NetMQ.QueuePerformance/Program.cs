using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NetMQ.QueuePerformance
{
    public class Program
    {
        public static void Main()
        {
            int count = 10000000;

            NetMQQueue<int> queue = new NetMQQueue<int>();

            var task = Task.Factory.StartNew(() =>
            {
                queue.Dequeue();

                Stopwatch stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < count; i++)
                {
                    queue.Dequeue();
                }

                stopwatch.Stop();

                Console.WriteLine("Dequeueing items per second: {0:N0}", count / stopwatch.Elapsed.TotalSeconds);
            });

            queue.Enqueue(-1);

            Stopwatch writeStopwatch = Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                queue.Enqueue(i);
            }

            writeStopwatch.Stop();

            Console.WriteLine("Enqueueing items per second: {0:N0}", count / writeStopwatch.Elapsed.TotalSeconds);

            task.Wait();
        }
    }
}
