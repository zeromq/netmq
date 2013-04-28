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

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Linq;

namespace NetMQ.zmq
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
				Handler = handler;
				Socket = socket;
				Cancelled = false;
			}
		}
		//  This table stores data for registered descriptors.
		private readonly List<PollSet> m_fdTable;

		private readonly List<PollSet> m_addList;

		//  If true, there's at least one retired event source.
		private bool m_retired;

		//  If true, thread is in the process of shutting down.
		volatile private bool m_stopping;
		volatile private bool m_stopped;

		private Thread m_worker;
		private readonly String m_name;

		readonly HashSet<Socket> m_checkRead = new HashSet<Socket>();
		readonly HashSet<Socket> m_checkWrite = new HashSet<Socket>();
		readonly HashSet<Socket> m_checkError = new HashSet<Socket>();


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

			m_fdTable = new List<PollSet>();
			m_addList = new List<PollSet>();
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

		public void AddFD(Socket fd, IPollEvents events)
		{
			m_addList.Add(new PollSet(fd, events));

			m_checkError.Add(fd);

			AdjustLoad(1);
		}


		public void RemoveFD(Socket handle)
		{
			PollSet pollSet;

			// if the socket was removed before being added there is no reason to mark retired, so just cancelling the socket and removing from add list 
			if ((pollSet = m_addList.FirstOrDefault(p => p.Socket == handle)) != null)
			{
				m_addList.Remove(pollSet);
				pollSet.Cancelled = true;
			}
			else
			{
				pollSet = m_fdTable.First(p => p.Socket == handle);
				pollSet.Cancelled = true;

				m_retired = true;
			}

			m_checkError.Remove(handle);
			m_checkRead.Remove(handle);
			m_checkWrite.Remove(handle);

			//  Decrease the load metric of the thread.
			AdjustLoad(-1);
		}


		public void SetPollin(Socket handle)
		{
			if (!m_checkRead.Contains(handle))
				m_checkRead.Add(handle);
		}


		public void ResetPollin(Socket handle)
		{
			m_checkRead.Remove(handle);
		}

		public void SetPollout(Socket handle)
		{
			if (!m_checkWrite.Contains(handle))
				m_checkWrite.Add(handle);
		}

		public void ResetPollout(Socket handle)
		{
			m_checkWrite.Remove(handle);
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
			ArrayList readList = new ArrayList();
			ArrayList writeList = new ArrayList();
			ArrayList errorList = new ArrayList();

			while (!m_stopping)
			{
				foreach (var pollSet in m_addList)
				{
					m_fdTable.Add(pollSet);
				}
				m_addList.Clear();

				//  Execute any due timers.
				int timeout = ExecuteTimers();

				readList.AddRange(m_checkRead.ToArray());
				writeList.AddRange(m_checkWrite.ToArray());
				errorList.AddRange(m_checkError.ToArray());

				try
				{
					Socket.Select(readList, writeList, errorList, timeout != 0 ? timeout * 1000 : -1);
				}
				catch (SocketException)
				{
					continue;
				}

				foreach (var pollSet in m_fdTable)
				{
					if (pollSet.Cancelled)
					{
						continue;
					}

					if (errorList.Contains(pollSet.Socket))
					{
						try
						{
							pollSet.Handler.InEvent();
						}
						catch (TerminatingException)
						{							
						}

					}

					if (pollSet.Cancelled)
					{
						continue;
					}

					if (writeList.Contains(pollSet.Socket))
					{
						try
						{
							pollSet.Handler.OutEvent();
						}
						catch (TerminatingException)
						{
						}
					}

					if (pollSet.Cancelled)
					{
						continue;
					}

					if (readList.Contains(pollSet.Socket))
					{
						try
						{
							pollSet.Handler.InEvent();
						}
						catch (TerminatingException)
						{
						}						
					}
				}

				errorList.Clear();
				writeList.Clear();
				readList.Clear();

				if (m_retired)
				{
					foreach (var item in m_fdTable.Where(k => k.Cancelled).ToList())
					{
						m_fdTable.Remove(item);
					}

					m_retired = false;
				}
			}
		}


	}
}
