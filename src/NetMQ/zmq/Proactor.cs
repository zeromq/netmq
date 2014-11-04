using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AsyncIO;

namespace NetMQ.zmq
{
    public class Proactor : PollerBase
    {
        private readonly string m_name;
        private CompletionPort m_completionPort;
        private Thread m_worker;
        private bool m_stopping;
        private bool m_stopped;

        private Dictionary<AsyncSocket, Item> m_sockets;

        class Item
        {
            public Item(IProcatorEvents procatorEvents)
            {
                ProcatorEvents = procatorEvents;
                Cancelled = false;
            }

            public IProcatorEvents ProcatorEvents { get; private set; }
            public bool Cancelled { get; set; }
        }

        public Proactor(string name)
        {
            m_name = name;
            m_stopping = false;
            m_stopped = false;
            m_completionPort = CompletionPort.Create();
            m_sockets = new Dictionary<AsyncSocket, Item>();
        }
        
        public void Start()
        {
            m_worker = new Thread(Loop);
            m_worker.IsBackground = true;
            m_worker.Name = m_name;
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
                {
                }

                m_completionPort.Dispose();
            }
        }

        public void SignalMailbox(IOThreadMailbox mailbox)
        {
            m_completionPort.Signal(mailbox);
        }

        public void AddSocket(AsyncSocket socket, IProcatorEvents procatorEvents)
        {
            var item = new Item(procatorEvents);
            m_sockets.Add(socket,item);

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

        private void Loop()
        {
            CompletionStatus completionStatus;

            while (!m_stopping)
            {
                //  Execute any due timers.
                int timeout = ExecuteTimers();

                if (m_completionPort.GetQueuedCompletionStatus(timeout != 0 ? timeout * 1000 : -1, out completionStatus))
                {
                    if (completionStatus.OperationType == OperationType.Signal)
                    {
                        IOThreadMailbox mailbox = (IOThreadMailbox)completionStatus.State;
                        mailbox.RaiseEvent();
                    }
                    // if the state is null we just ignore the completion status
                    else if (completionStatus.State != null)
                    {
                        Item item = (Item)completionStatus.State;

                        if (!item.Cancelled)
                        {
                            try
                            {
                                switch (completionStatus.OperationType)
                                {
                                    case OperationType.Accept:
                                    case OperationType.Receive:
                                        item.ProcatorEvents.InCompleted(completionStatus.SocketError,
                                            completionStatus.BytesTransferred);
                                        break;
                                    case OperationType.Connect:
                                    case OperationType.Disconnect:
                                    case OperationType.Send:
                                        item.ProcatorEvents.OutCompleted(completionStatus.SocketError,
                                            completionStatus.BytesTransferred);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            catch (TerminatingException)
                            {
                            }
                        }
                    }
                }
            }
        }
    }
}
