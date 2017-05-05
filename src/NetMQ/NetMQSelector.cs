using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using JetBrains.Annotations;
using NetMQ.Core;
using NetMQ.Core.Utils;

namespace NetMQ
{
    /// <summary>
    /// For selecting on <see cref="NetMQSocket"/> and regular .NET <see cref="Socket"/> objects.
    /// </summary>
    /// <remarks>
    /// This is for advanced scenarios only.
    /// Most use cases are better served by <see cref="NetMQPoller"/>.
    /// </remarks>
    public sealed class NetMQSelector
    {
        private readonly List<Socket> m_checkRead = new List<Socket>();
        private readonly List<Socket> m_checkWrite = new List<Socket>();
        private readonly List<Socket> m_checkError = new List<Socket>();

        /// <summary>
        /// Selector Item used to hold the NetMQSocket/Socket and PollEvents
        /// </summary>
        public sealed class Item
        {
            public Item(NetMQSocket socket, PollEvents @event)
            {
                Socket = socket;
                Event = @event;
            }

            public Item(Socket fileDescriptor, PollEvents @event)
            {
                FileDescriptor = fileDescriptor;
                Event = @event;
            }

            public Socket FileDescriptor { get; }
            public NetMQSocket Socket { get; }
            public PollEvents Event { get; }
            public PollEvents ResultEvent { get; set; }
        }

        /// <summary>
        /// Select on NetMQSocket or Socket, similar behavior to Socket.Select.
        /// </summary>
        /// <param name="items">Items to select on (must not be null)</param>
        /// <param name="itemsCount">Number of items in the array to select on</param>
        /// <param name="timeout">a time-out period, in milliseconds</param>
        /// <returns></returns>
        /// <exception cref="FaultException">The internal select operation failed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public bool Select([NotNull] Item[] items, int itemsCount, long timeout)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (itemsCount == 0)
                return false;

            bool firstPass = true;
            int numberOfEvents = 0;

            Stopwatch stopwatch = null;

            while (true)
            {
                long currentTimeoutMicroSeconds;

                if (firstPass)
                {
                    currentTimeoutMicroSeconds = 0;
                }
                else if (timeout < 0)
                {
                    // Consider everything below 0 to be infinite
                    currentTimeoutMicroSeconds = -1;
                }
                else
                {
                    currentTimeoutMicroSeconds = (timeout - stopwatch.ElapsedMilliseconds) * 1000;

                    if (currentTimeoutMicroSeconds < 0)
                    {
                        currentTimeoutMicroSeconds = 0;
                    }
                    else if (currentTimeoutMicroSeconds > int.MaxValue)
                    {
                        currentTimeoutMicroSeconds = int.MaxValue;
                    }
                }

                m_checkRead.Clear();
                m_checkWrite.Clear();
                m_checkError.Clear();

                for (int i = 0; i < itemsCount; i++)
                {
                    var pollItem = items[i];

                    if (pollItem.Socket != null)
                    {
                        if (pollItem.Event != PollEvents.None && pollItem.Socket.SocketHandle.Handle.Connected)
                            m_checkRead.Add(pollItem.Socket.SocketHandle.Handle);
                    }
                    else
                    {
                        if (pollItem.Event.HasIn())
                            m_checkRead.Add(pollItem.FileDescriptor);

                        if (pollItem.Event.HasOut())
                            m_checkWrite.Add(pollItem.FileDescriptor);
                    }
                }

                try
                {
                    SocketUtility.Select(m_checkRead, m_checkWrite, m_checkError, (int)currentTimeoutMicroSeconds);
                }
                catch (SocketException x)
                {
#if DEBUG
                    string textOfListRead = StringLib.AsString(m_checkRead);
                    string textOfListWrite = StringLib.AsString(m_checkWrite);
                    string textOfListError = StringLib.AsString(m_checkError);
                    string xMsg = $"In Selector.Select, Socket.Select({textOfListRead}, {textOfListWrite}, {textOfListError}, {currentTimeoutMicroSeconds}) threw a SocketException: {x.Message}";
                    Debug.WriteLine(xMsg);
                    throw new FaultException(innerException: x, message: xMsg);
#else
                    throw new FaultException(innerException: x, message: "Within SocketUtility.Select");
#endif
                }

                for (int i = 0; i < itemsCount; i++)
                {
                    var selectItem = items[i];

                    selectItem.ResultEvent = PollEvents.None;

                    if (selectItem.Socket != null)
                    {
                        var events = (PollEvents)selectItem.Socket.GetSocketOption(ZmqSocketOption.Events);

                        if (selectItem.Event.HasIn() && events.HasIn())
                            selectItem.ResultEvent |= PollEvents.PollIn;

                        if (selectItem.Event.HasOut() && events.HasOut())
                            selectItem.ResultEvent |= PollEvents.PollOut;
                    }
                    else
                    {
                        if (m_checkRead.Contains(selectItem.FileDescriptor))
                            selectItem.ResultEvent |= PollEvents.PollIn;

                        if (m_checkWrite.Contains(selectItem.FileDescriptor))
                            selectItem.ResultEvent |= PollEvents.PollOut;
                    }

                    if (selectItem.ResultEvent != PollEvents.None)
                        numberOfEvents++;
                }

                if (timeout == 0)
                    break;

                if (numberOfEvents > 0)
                    break;

                if (timeout < 0)
                {
                    if (firstPass)
                        firstPass = false;

                    continue;
                }

                if (firstPass)
                {
                    stopwatch = Stopwatch.StartNew();
                    firstPass = false;
                    continue;
                }

                // Check also equality as it might frequently occur on 1000Hz clock
                if (stopwatch.ElapsedMilliseconds >= timeout)
                    break;
            }

            return numberOfEvents > 0;
        }
    }
}
