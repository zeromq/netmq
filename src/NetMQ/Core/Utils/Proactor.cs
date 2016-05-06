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

        private readonly string m_name;
        private readonly CompletionPort m_completionPort;
        private Thread m_worker;
        private bool m_stopping;
        private bool m_stopped;

        private readonly Dictionary<AsyncSocket, Item> m_sockets;

        private class Item
        {
            public Item([NotNull] IProactorEvents proactorEvents)
            {
                ProactorEvents = proactorEvents;
                Cancelled = false;
            }

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
            var completionStatuses = new CompletionStatus[CompletionStatusArraySize];

            while (!m_stopping)
            {
                // Execute any due timers.
                int timeout = ExecuteTimers();

                int removed;

                if (!m_completionPort.GetMultipleQueuedCompletionStatus(timeout != 0 ? timeout : -1, completionStatuses, out removed))
                    continue;

                for (int i = 0; i < removed; i++)
                {
                    try
                    {
                        if (completionStatuses[i].OperationType == OperationType.Signal)
                        {
                            var mailbox = (IOThreadMailbox)completionStatuses[i].State;
                            mailbox.RaiseEvent();
                        }
                            // if the state is null we just ignore the completion status
                        else if (completionStatuses[i].State != null)
                        {
                            var item = (Item)completionStatuses[i].State;

                            if (!item.Cancelled)
                            {
                                    switch (completionStatuses[i].OperationType)
                                    {
                                        case OperationType.Accept:
                                        case OperationType.Receive:
                                            item.ProactorEvents.InCompleted(
                                                completionStatuses[i].SocketError,
                                                completionStatuses[i].BytesTransferred);
                                            break;
                                        case OperationType.Connect:
                                        case OperationType.Disconnect:
                                        case OperationType.Send:
                                            item.ProactorEvents.OutCompleted(
                                                completionStatuses[i].SocketError,
                                                completionStatuses[i].BytesTransferred);
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