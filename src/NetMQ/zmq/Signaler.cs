/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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
using System.Net.Sockets;
using System.Threading;
using System.Security.AccessControl;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

//  This is a cross-platform equivalent to signal_fd. However, as opposed
//  to signal_fd there can be at most one signal in the signaler at any
//  given moment. Attempt to send a signal before receiving the previous
//  one will result in undefined behaviour.

namespace NetMQ.zmq
{
	public class Signaler
	{
		//  Underlying write & read file descriptor.
		private Socket m_w;
		private Socket m_r;

		public Signaler()
		{
			//  Create the socketpair for signaling.
			MakeFDpair();

			//  Set both fds to non-blocking mode.
			m_w.Blocking = false;
			m_r.Blocking = false;
		}

		public void Close()
		{
			try
			{
				m_w.Close();
			}
			catch (Exception)
			{
			}

			try
			{
				m_r.Close();
			}
			catch (Exception)
			{
			}
		}
	
		//  Creates a pair of filedescriptors that will be used
		//  to pass the signals.
		private void MakeFDpair()
		{
			Mutex sync;

			try
			{
				sync = new Mutex(false, "Global\\netmq-signaler-port-sync");
			}
			catch (UnauthorizedAccessException)
			{
				sync = Mutex.OpenExisting("Global\\netmq-signaler-port-sync", MutexRights.Synchronize | MutexRights.Modify);
			}

			Debug.Assert(sync != null);

			sync.WaitOne();

			Socket listner = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified);
			listner.NoDelay = true;
			listner.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

			IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, Config.SignalerPort);

			listner.Bind(endpoint);
			listner.Listen(1);

			m_w = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified);
			
			m_w.NoDelay = true;

			m_w.Connect(endpoint);

			m_r = listner.Accept();			

			listner.Close();

			sync.ReleaseMutex();

			// Release the kernel object
			sync.Dispose();
		}

		public Socket FD
		{
			get
			{
				return m_r;
			}
		}

		public void Send()
		{
			byte[] dummy = new byte[1] { 0 };
			int nbytes = m_w.Send(dummy);

			Debug.Assert(nbytes == 1);
		}

		public bool WaitEvent(int timeout)
		{
			return m_r.Poll(timeout * 1000, SelectMode.SelectRead);
		}

		public void Recv()
		{
			byte[] dummy = new byte[1];

			int nbytes = m_r.Receive(dummy);

			Debug.Assert(nbytes == 1);
			Debug.Assert(dummy[0] == 0);
		}




	}
}
