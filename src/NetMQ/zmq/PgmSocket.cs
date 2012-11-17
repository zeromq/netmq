using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace NetMQ.zmq
{	
	public class PgmSocket
	{
		public static readonly int PROTOCOL_TYPE_NUMBER = 113;
		public static readonly ProtocolType PGM_PROTOCOL_TYPE = (ProtocolType)113;
		public static readonly SocketOptionLevel PGM_LEVEL = (SocketOptionLevel)PROTOCOL_TYPE_NUMBER;

		public static readonly SocketOptionName EnableGigabitOption = (SocketOptionName) 1014;

		private readonly Options m_options;

		public PgmSocket(Options options)
		{
			m_options = options;
		}

		internal void Init()
		{
			FD = new Socket(AddressFamily.InterNetwork, SocketType.Rdm, PGM_PROTOCOL_TYPE);
		}

		internal void EnableGigabit()
		{
			FD.SetSocketOption(PGM_LEVEL, EnableGigabitOption, BitConverter.GetBytes((uint)1));		
		}

		internal void Init(Socket fd)
		{
			FD = fd;
		}


		public Socket FD { get; private set; }
	}
}
