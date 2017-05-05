using System;
using System.Collections.Generic;
using System.Threading;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Core.Utils
{
    internal class Proactor : PollerBase
    {
        private const int CompletionStatusArraySize = 100;

        private readonly Dictionary<AsyncSocket, Item> m_sockets;
        private readonly CompletionPort m_completionPort;
        private readonly string m_name;

        private Thread m_worker;
        private bool m_stopping;
        private bool m_stopped;

        private class Item
        {
            public Item([NotNull] IProactorEvents proactorEvents) => ProactorEvents = proactorEvents;

            [NotNull]
            public IProactorEvents ProactorEvents { get; }
            public bool Cancelled { get; set; }
        }

        public Proactor([NotNull] string name)
        {
            m_name = name;
            m_stopping = false;
            m_stopped = false;
            m_completionPort = CompletionPort.Create();
            m_sockets = new Dictionary<AsyncSocket, Item>();
        }

        public void Start()
        {
            m_worker = new Thread(Loop) { IsBackground = true, Name = m_name };
            m_worker.Start();
        }

        public void Stop()
        {
            m_stopping = true;
        }

        public void Destroy()
        {
            if (!m_stopped)
            {
                try
                {
                    m_worker.Join();
                }
                catch (Exception)
                {}

                m_stopped = true;

                m_completionPort.Dispose();
            }
        }

        public void SignalMailbox(IOThreadMailbox mailbox)
        {
            m_completionPort.Signal(mailbox);
        }

        public void AddSocket(AsyncSocket socket, IProactorEvents proactorEvents)
        {
            var item = new Item(proactorEvents);
            m_sockets.Add(socket, item);

            m_completionPort.AssociateSocket(socket, item);
            AdjustLoad(1);
        }

        public void RemoveSocket(AsyncSocket socket)
        {
            AdjustLoad(-1);

            var item = m_sockets[socket];
            m_sockets.Remove(socket);
            item.Cancelled = true;
        }

        /// <exception cref="ArgumentOutOfRangeException">The completionStatuses item must have a valid OperationType.</exception>
        private void Loop()
        {
            var completions = new CompletionStatus[CompletionStatusArraySize];

            while (!m_stopping)
            {
                // Execute any due timers.
                int timeout = ExecuteTimers();

                if (!m_completionPort.GetMultipleQueuedCompletionStatus(timeout != 0 ? timeout : -1, completions, out int removed))
                    continue;

                for (int i = 0; i < removed; i++)
                {
                    try
                    {
                        var completion = completions[i];
                        if (completion.OperationType == OperationType.Signal)
                        {
                            var mailbox = (IOThreadMailbox)completion.State;
                            mailbox.RaiseEvent();
                        }
                            // if the state is null we just ignore the completion status
                        else if (completion.State != null)
                        {
                            var item = (Item)completion.State;

                            if (!item.Cancelled)
                            {
                                switch (completion.OperationType)
                                {
                                    case OperationType.Accept:
                                    case OperationType.Receive:
                                        item.ProactorEvents.InCompleted(
                                            completion.SocketError,
                                            completion.BytesTransferred);
                                        break;
                                    case OperationType.Connect:
                                    case OperationType.Disconnect:
                                    case OperationType.Send:
                                        item.ProactorEvents.OutCompleted(
                                            completion.SocketError,
                                            completion.BytesTransferred);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }
                    catch (TerminatingException)
                    { }
                }
            }
        }
    }
}