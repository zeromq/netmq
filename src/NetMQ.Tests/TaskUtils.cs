using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetMQ.Tests
{
    internal class TaskUtils
    {
        internal static async Task PollUntil(Func<bool> condition, TimeSpan timeout)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            await PollUntil(condition, cts.Token);
        }

        internal static async Task PollUntil(Func<bool> condition, CancellationToken ct = default)
        {
            try
            {
                while (!condition())
                {
                    await Task.Delay(25, ct).ConfigureAwait(true);
                }
            }
            catch (TaskCanceledException)
            {
                // Task was cancelled. Ignore exception and return.
            }
        }

        internal static bool WaitAll(IEnumerable<Task> tasks, TimeSpan timeout)
        {
            PollUntil(() => tasks.All(t => t.IsCompleted), timeout).Wait();
            return tasks.All(t => t.Status == TaskStatus.RanToCompletion);
        }

        internal static void WaitAll(IEnumerable<Task> tasks)
        {
            PollUntil(() => tasks.All(t => t.IsCompleted), Timeout.InfiniteTimeSpan).Wait();
        }

        internal static bool Wait(Task task, TimeSpan timeout)
        {
            PollUntil(() => task.IsCompleted, timeout).Wait();
            return task.Status == TaskStatus.RanToCompletion;
        }

        internal static void Wait(Task task)
        {
            PollUntil(() => task.IsCompleted, Timeout.InfiniteTimeSpan).Wait();
        }
    }
}
