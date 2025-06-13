#if !NET35
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NetMQ.Sockets;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NetMQ
{
    /// <summary>
    /// Events args for NetMQQueue
    /// </summary>
    /// <typeparam name="T">The type of the queue</typeparam>
    public sealed class NetMQQueueEventArgs<T> : EventArgs
    {
        internal NetMQQueueEventArgs(NetMQQueue<T> queue) => Queue = queue;
        
        /// <summary>
        /// The queue that invoked the event 
        /// </summary>
        public NetMQQueue<T> Queue { get; }
    }

    /// <summary>
    /// Multi producer single consumer queue which you can poll on with a Poller.
    /// </summary>
    /// <typeparam name="T">Type of the item in queue</typeparam>
    public sealed class NetMQQueue<T> : IDisposable, ISocketPollable, IEnumerable<T>
    {
        private readonly PairSocket m_writer;
        private readonly PairSocket m_reader;
        private readonly ConcurrentQueue<T> m_queue;
        private readonly EventDelegator<NetMQQueueEventArgs<T>> m_eventDelegator;
        private Msg m_dequeueMsg;

        /// <summary>
        /// Create new NetMQQueue.
        /// </summary>
        /// <param name="capacity">The capacity of the queue, use zero for unlimited</param>
        public NetMQQueue(int capacity = 0)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            m_queue = new ConcurrentQueue<T>();
            PairSocket.CreateSocketPair(out m_writer,
                                        out m_reader,
                                        writer => writer.Options.SendHighWatermark = capacity / 2,
                                        reader => reader.Options.ReceiveHighWatermark = capacity / 2);

            m_eventDelegator = new EventDelegator<NetMQQueueEventArgs<T>>(
                () => m_reader.ReceiveReady += OnReceiveReady,
                () => m_reader.ReceiveReady -= OnReceiveReady);

            m_dequeueMsg = new Msg();
            m_dequeueMsg.InitEmpty();
        }

        private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
        {
            m_eventDelegator.Fire(this, new NetMQQueueEventArgs<T>(this));
        }

        /// <summary>
        /// Register for this event for notification when there are items in the queue. Queue must be added to a poller for this to work.
        /// </summary>
        public event EventHandler<NetMQQueueEventArgs<T>> ReceiveReady
        {
            add => m_eventDelegator.Event += value;
            remove => m_eventDelegator.Event -= value;
        }

        NetMQSocket ISocketPollable.Socket => m_reader;
        
        /// <summary>
        /// Returns true if the queue is disposed
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets the number of items contained in the queue.
        /// </summary>
        public int Count => m_queue.Count;

        /// <summary>
        /// Gets a value that indicates whether the queue is empty.
        /// </summary>
        public bool IsEmpty => m_queue.IsEmpty;

        /// <summary>
        /// Try to dequeue an item from the queue. Dequeueing and item is not thread safe.
        /// </summary>
        /// <param name="result">Will be filled with the item upon success</param>
        /// <param name="timeout">Timeout to try and dequeue and item</param>
        /// <returns>Will return false if it didn't succeed to dequeue an item after the timeout.</returns>
        public bool TryDequeue([MaybeNullWhen(false)] out T result, TimeSpan timeout)
        {
            if (m_reader.TryReceive(ref m_dequeueMsg, timeout))
            {
                return m_queue.TryDequeue(out result);
            }
            else
            {
                result = default(T);
                return false;
            }
        }

        /// <summary>
        /// Dequeue an item from the queue, will block if queue is empty. Dequeueing and item is not thread safe.
        /// </summary>
        /// <returns>Dequeued item</returns>
        public T? Dequeue()
        {
            m_reader.TryReceive(ref m_dequeueMsg, SendReceiveConstants.InfiniteTimeout);

            m_queue.TryDequeue(out T? result);

            return result;
        }

        /// <summary>
        /// Enqueue an item to the queue, will block if the queue is full.
        /// </summary>
        /// <param name="value"></param>
        public void Enqueue(T value)
        {
            m_queue.Enqueue(value);

            var msg = new Msg();
            msg.InitGC(EmptyArray<byte>.Instance, 0);

            lock (m_writer)
                m_writer.TrySend(ref msg, SendReceiveConstants.InfiniteTimeout, false);

            msg.Close();
        }

        #region IEnumerator

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => m_queue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        /// <summary>
        /// Dispose the queue.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            m_eventDelegator.Dispose();
            m_writer.Dispose();
            m_reader.Dispose();
            m_dequeueMsg.Close();

            IsDisposed = true;
        }
    }
}
#endif
