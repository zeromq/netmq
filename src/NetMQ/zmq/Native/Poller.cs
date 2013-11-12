/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
#if PRE_4
using System.Linq;
#else
using System.Runtime.InteropServices;
#endif


namespace NetMQ.zmq.Native
{
    public class Poller : PollerBase
    {
        private class PollSet
        {
            public Socket Socket { get; private set; }
            public IPollEvents Handler { get; private set; }
            public bool Cancelled { get; set; }

            public PollSet(Socket socket, IPollEvents handler)
            {
                Socket = socket;
                Handler = handler;
                Cancelled = false;
            }
        }
        //  This table stores data for registered descriptors.
        private readonly IList<PollSet> m_fds;
        private readonly IList<PollSet> m_tempFDs;

        //  If true, there's at least one retired event source.
        private bool m_retired;

        private bool m_new;

        //  If true, thread is in the process of shutting down.
        volatile private bool m_stopping;
        volatile private bool m_stopped;

        private Thread m_worker;
        private readonly String m_name;

        private int m_readCount = 0;
        private int m_writeCount = 0;
        private int m_errorCount = 0;

        private readonly IntPtr[] m_sourceSetIn = new IntPtr[1024];
        private readonly IntPtr[] m_sourceSetOut = new IntPtr[1024];
        private readonly IntPtr[] m_sourceSetError = new IntPtr[1024];

        private readonly IntPtr[] m_readFDs = new IntPtr[1024];
        private readonly IntPtr[] m_writeFDs = new IntPtr[1024];
        private readonly IntPtr[] m_errorFDs = new IntPtr[1024];


        public Poller()
            : this("poller")
        {

        }

        public Poller(String name)
        {
            m_name = name;
            m_retired = false;
            m_stopping = false;
            m_stopped = false;
            m_new = false;

            m_fds = new List<PollSet>();
            m_tempFDs = new List<PollSet>();
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
            }
        }

        private void FDClear(Socket fd, IntPtr[] set, ref int count)
        {
            // we always ignore the first element in the array because it used for the counter

            int i;
            for (i = 0; i < count; i++)
            {
                if (set[i + 1] == fd.Handle)
                {
                    while (i < count - 1)
                    {
                        set[i + 1] = set[i + 2];
                        i++;
                    }
                    count--;
                    break;
                }
            }
        }

        private void FDSet(Socket fd, IntPtr[] set, ref int count)
        {
            // we always ignore the first element in the array because it used for the counter

            int i;
            for (i = 0; i < count; i++)
            {
                if (set[i + 1] == fd.Handle)
                {
                    break;
                }
            }
            if (i == count)
            {
                if (count < set.Length)
                {
                    set[i + 1] = fd.Handle;
                    count++;
                }
            }
        }

        private bool FDIsSet(Socket fd, IntPtr[] set)
        {
            // we always ignore the first element in the array because it used for the counter

            int count = (int)set[0];

            for (int i = 0; i < count; i++)
            {
                if (set[i + 1] == fd.Handle)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddFD(Socket fd, IPollEvents events)
        {
            m_tempFDs.Add(new PollSet(fd, events));

            FDSet(fd, m_sourceSetError, ref m_errorCount);

            m_new = true;

            AdjustLoad(1);
        }


        public void RemoveFD(Socket handle)
        {
            PollSet pollset = m_fds.FirstOrDefault(p => p.Socket == handle);

            if (pollset == null)
            {
                pollset = m_tempFDs.First(p => p.Socket == handle);
            }

            // Debug.Assert(pollset != null);

            pollset.Cancelled = true;
            m_retired = true;

            FDClear(handle, m_sourceSetIn, ref m_readCount);
            FDClear(handle, m_sourceSetOut, ref m_writeCount);
            FDClear(handle, m_sourceSetError, ref m_errorCount);

            // Decrease the load metric of the thread.
            AdjustLoad(-1);
        }


        public void SetPollin(Socket handle)
        {
            FDSet(handle, m_sourceSetIn, ref m_readCount);
        }


        public void ResetPollin(Socket handle)
        {
            FDClear(handle, m_sourceSetIn, ref m_readCount);
        }

        public void SetPollout(Socket handle)
        {
            FDSet(handle, m_sourceSetOut, ref m_writeCount);
        }

        public void ResetPollout(Socket handle)
        {
            FDClear(handle, m_sourceSetOut, ref m_writeCount);
        }

        public void Start()
        {
            m_worker = new Thread(Loop);
            m_worker.Name = m_name;
            m_worker.Start();
        }

        public void Stop()
        {
            m_stopping = true;
        }

        public void Loop()
        {
            while (!m_stopping)
            {
                if (m_new)
                {
                    foreach (PollSet pollSet in m_tempFDs)
                    {
                        m_fds.Add(pollSet);
                    }
                    m_tempFDs.Clear();
                    m_new = false;
                }

                //  Execute any due timers.
                int timeout = ExecuteTimers();

                Array.Copy(m_sourceSetIn, 1, m_readFDs, 1, m_readCount);
                Array.Copy(m_sourceSetOut, 1, m_writeFDs, 1, m_writeCount);
                Array.Copy(m_sourceSetError, 1, m_errorFDs, 1, m_errorCount);

                m_readFDs[0] = (IntPtr)m_readCount;
                m_writeFDs[0] = (IntPtr)m_writeCount;
                m_errorFDs[0] = (IntPtr)m_errorCount;

                NativeMethods.TimeValue tv = new NativeMethods.TimeValue(timeout / 1000, timeout % 1000 * 1000);

                int rc;

                if (timeout != 0)
                {
                    rc = NativeMethods.@select(0, m_readFDs, m_writeFDs, m_errorFDs, ref tv);
                }
                else
                {
                    rc = NativeMethods.@select(0, m_readFDs, m_writeFDs, m_errorFDs, IntPtr.Zero);
                }

                if (rc == -1)
                {
                    throw new SocketException();
                }

                // Debug.Assert(rc != -1);

                if (rc == 0)
                {
                    continue;
                }

                foreach (var item in m_fds)
                {
                    if (item.Cancelled)
                    {
                        continue;
                    }

                    if (FDIsSet(item.Socket, m_errorFDs))
                    {
                        item.Handler.InEvent();
                    }

                    if (item.Cancelled)
                    {
                        continue;
                    }

                    if (FDIsSet(item.Socket, m_writeFDs))
                    {
                        item.Handler.OutEvent();
                    }

                    if (item.Cancelled)
                    {
                        continue;
                    }

                    if (FDIsSet(item.Socket, m_readFDs))
                    {
                        item.Handler.InEvent();
                    }
                }

                if (m_retired)
                {
                    foreach (var item in m_fds.Where(k => k.Cancelled).ToList())
                    {
                        m_fds.Remove(item);
                    }

                    foreach (var item in m_tempFDs.Where(k => k.Cancelled).ToList())
                    {
                        m_tempFDs.Remove(item);
                    }

                    m_retired = false;
                }
            }
        }


    }
}
