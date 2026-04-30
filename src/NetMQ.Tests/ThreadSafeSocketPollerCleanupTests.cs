using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    /// <summary>
    /// Regression tests for the thread-safety issue where closing a thread-safe socket
    /// (e.g. <see cref="ServerSocket"/>) while a user thread concurrently processes a
    /// pending Bind command (via TrySend/TryRecv -> ProcessCommands -> AttachPipe) could
    /// cause an <see cref="ArgumentOutOfRangeException"/> in SocketBase.ProcessTerm.
    ///
    /// The root cause was iterating the live <c>m_pipes</c> list on the reaper thread
    /// while a user thread added to it under <c>m_threadSafeSync</c>.  The fix uses
    /// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> with
    /// <see cref="System.Collections.Immutable.ImmutableInterlocked"/> so that every
    /// read in ProcessTerm gets an immutable snapshot and concurrent Add/Remove produce
    /// a new array without disturbing an in-progress iteration.
    /// </summary>
    public class ThreadSafeSocketPollerCleanupTests : IClassFixture<CleanupAfterFixture>
    {
        public ThreadSafeSocketPollerCleanupTests() => NetMQConfig.Cleanup();

        /// <summary>
        /// Repeatedly creates a <see cref="ServerSocket"/>, connects several
        /// <see cref="ClientSocket"/>s to it, sends messages concurrently from multiple
        /// threads (to keep the user thread processing commands), then disposes the server
        /// while new clients are still connecting.
        ///
        /// Without the fix this reliably throws ArgumentOutOfRangeException inside
        /// SocketBase.ProcessTerm when run enough iterations.
        /// </summary>
        [Fact]
        public async Task ClosingServerWhileClientsConnectDoesNotCrash()
        {
            const int iterations = 20;
            const int clientsPerIteration = 5;

            for (int i = 0; i < iterations; i++)
            {
                NetMQConfig.Cleanup();

                using var server = new ServerSocket();
                int port = server.BindRandomPort("tcp://127.0.0.1");

                // Start a task that reads from the server concurrently, keeping the
                // thread-safe socket's internal command queue being processed.
                var cts = new CancellationTokenSource();
                var readerTask = Task.Run(() =>
                {
                    try
                    {
                        var msg = new Msg();
                        msg.InitEmpty();
                        while (!cts.IsCancellationRequested)
                        {
                            if (server.TryReceive(ref msg, TimeSpan.FromMilliseconds(1)))
                                msg.Close();
                        }
                    }
                    catch (ObjectDisposedException) { }
                    catch (OperationCanceledException) { }
                });

                // Connect several clients.  Each successful TCP handshake will enqueue a
                // Bind command on the server's mailbox that ProcessCommands will turn into
                // AttachPipe -> m_pipes.Add().
                var clients = new ClientSocket[clientsPerIteration];
                for (int c = 0; c < clientsPerIteration; c++)
                {
                    clients[c] = new ClientSocket();
                    clients[c].Connect($"tcp://127.0.0.1:{port}");
                }

                // Give a short window for connections to arrive and pending Bind commands
                // to queue up in the server's mailbox.
                await Task.Delay(5);

                // Dispose the server.  This triggers Close -> Reap -> StartReaping ->
                // Terminate -> ProcessTerm.  If the fix is absent, ProcessTerm iterating
                // the live m_pipes list while the reader task's ProcessCommands concurrently
                // calls AttachPipe causes the crash.
                cts.Cancel();
                server.Dispose();

                await readerTask;

                foreach (var client in clients)
                    client.Dispose();
            }
        }
    }
}
