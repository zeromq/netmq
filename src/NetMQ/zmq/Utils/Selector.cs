using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;

namespace NetMQ.zmq.Utils
{
    class SelectItem
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

        public Socket FileDescriptor { get;private set; } 
        public SocketBase Socket { get; private set; }

        public PollEvents Event { get; private set; }

        public PollEvents ResultEvent { get; set; }
    }

    class Selector
    {
        private List<Socket> m_checkRead;
        private List<Socket> m_checkWrite;
        private List<Socket> m_checkError;                       

        public Selector()
        {
            m_checkRead = new List<Socket>();
            m_checkWrite= new List<Socket>();
            m_checkError = new List<Socket>();
        }

        public bool Select(SelectItem[] items, int itemsCount, int timeout)
        {
            if (items == null)
            {
                throw new FaultException();
            }
            if (itemsCount == 0)
            {
                if (timeout == 0)
                    return false;
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
                catch (SocketException)
                {
                    throw new FaultException();
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
