using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetMQ.zmq.Utils;

namespace NetMQ.zmq.Utils
{
    /// <summary>
    /// A SelectItem is a pairing of a (Socket or SocketBase) and a PollEvents value.
    /// </summary>
    internal class SelectItem
    {
        public SelectItem(SocketBase socket, PollEvents @event)
        {
            Socket = socket;
            Event = @event;
        }

        public SelectItem(Socket fileDescriptor, PollEvents @event)
        {
            FileDescriptor = fileDescriptor;
            Event = @event;
        }

        public Socket FileDescriptor { get; private set; }

        public SocketBase Socket { get; private set; }

        public PollEvents Event { get; private set; }

        public PollEvents ResultEvent { get; set; }
    }

    /// <summary>
    /// A Selector holds three lists of Sockets - for read, write, and error,
    /// and provides a Select method.
    /// </summary>
    internal class Selector
    {
        private readonly List<Socket> m_checkRead;
        private readonly List<Socket> m_checkWrite;
        private readonly List<Socket> m_checkError;

        /// <summary>
        /// Create a new Selector object which holds three (empty) lists of Sockets.
        /// </summary>
        public Selector()
        {
            m_checkRead = new List<Socket>();
            m_checkWrite = new List<Socket>();
            m_checkError = new List<Socket>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items">  (must not be null)</param>
        /// <param name="itemsCount"></param>
        /// <param name="timeout">a time-out period, in milliseconds</param>
        /// <returns></returns>
        public bool Select(SelectItem[] items, int itemsCount, int timeout)
        {
            if (items == null)
            {
                throw new FaultException("Selector.Select called with items equal to null.");
            }
            if (itemsCount == 0)
            {
                if (timeout == 0)
                    return false;
                //TODO:  Do we really want to simply sleep and return, doing nothing during this interval?
                Thread.Sleep(timeout);
                return false;
            }

            bool firstPass = true;
            int numberOfEvents = 0;

            Stopwatch stopwatch = null;

            while (true)
            {
                int currentTimeoutMicroSeconds;

                if (firstPass)
                {
                    currentTimeoutMicroSeconds = 0;
                }
                else if (timeout == -1)
                {
                    currentTimeoutMicroSeconds = -1;
                }
                else
                {
                    currentTimeoutMicroSeconds = (int)((timeout - stopwatch.ElapsedMilliseconds) * 1000);

                    if (currentTimeoutMicroSeconds < 0)
                    {
                        currentTimeoutMicroSeconds = 0;
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
                        if (pollItem.Event != PollEvents.None && pollItem.Socket.Handle.Connected)
                        {
                            m_checkRead.Add(pollItem.Socket.Handle);
                        }
                    }
                    else
                    {
                        if ((pollItem.Event & PollEvents.PollIn) == PollEvents.PollIn)
                        {
                            m_checkRead.Add(pollItem.FileDescriptor);
                        }

                        if ((pollItem.Event & PollEvents.PollOut) == PollEvents.PollOut)
                        {
                            m_checkWrite.Add(pollItem.FileDescriptor);
                        }
                    }
                }

                try
                {
                    SocketUtility.Select(m_checkRead, m_checkWrite, m_checkError, currentTimeoutMicroSeconds);
                }
                catch (SocketException x)
                {
#if DEBUG
                    string textOfListRead = StringLib.AsString(m_checkRead);
                    string textOfListWrite = StringLib.AsString(m_checkWrite);
                    string textOfListError = StringLib.AsString(m_checkError);
                    string xMsg = String.Format("In Selector.Select, Socket.Select({0}, {1}, {2}, {3}) threw a SocketException: {4}", textOfListRead, textOfListWrite, textOfListError, currentTimeoutMicroSeconds, x.Message);
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
                        PollEvents events = (PollEvents)selectItem.Socket.GetSocketOption(ZmqSocketOptions.Events);

                        if ((selectItem.Event & PollEvents.PollIn) == PollEvents.PollIn && (events & PollEvents.PollIn) == PollEvents.PollIn)
                        {
                            selectItem.ResultEvent |= PollEvents.PollIn;
                        }

                        if ((selectItem.Event & PollEvents.PollOut) == PollEvents.PollOut &&
                            (events & PollEvents.PollOut) == PollEvents.PollOut)
                        {
                            selectItem.ResultEvent |= PollEvents.PollOut;
                        }
                    }
                    else
                    {
                        if (m_checkRead.Contains(selectItem.FileDescriptor))
                        {
                            selectItem.ResultEvent |= PollEvents.PollIn;
                        }

                        if (m_checkWrite.Contains(selectItem.FileDescriptor))
                        {
                            selectItem.ResultEvent |= PollEvents.PollOut;
                        }
                    }

                    if (selectItem.ResultEvent != PollEvents.None)
                    {
                        numberOfEvents++;
                    }
                }

                if (timeout == 0)
                {
                    break;
                }

                if (numberOfEvents > 0)
                {
                    break;
                }

                if (timeout < 0)
                {
                    if (firstPass)
                    {
                        firstPass = false;
                    }

                    continue;
                }

                if (firstPass)
                {
                    stopwatch = Stopwatch.StartNew();
                    firstPass = false;
                    continue;
                }

                if (stopwatch.ElapsedMilliseconds > timeout)
                {
                    break;
                }
            }

            return numberOfEvents > 0;
        }
    }
}
