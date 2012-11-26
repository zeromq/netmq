using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace NetMQ.zmq
{
	public enum PgmSocketType
	{
		Publisher,
		Receiver,
		Listener
	}

	public struct RM_SEND_WINDOW
	{
		public uint RateKbitsPerSec; // Send rate
		public uint WindowSizeInMSecs;
		public uint WindowSizeInBytes;
	}

	public class PgmSocket
	{
		public static readonly int PROTOCOL_TYPE_NUMBER = 113;
		public static readonly ProtocolType PGM_PROTOCOL_TYPE = (ProtocolType)113;
		public static readonly SocketOptionLevel PGM_LEVEL = (SocketOptionLevel)PROTOCOL_TYPE_NUMBER;

		public static readonly int RM_OPTIONSBASE = 1000;

		// Set/Query rate (Kb/Sec) + window size (Kb and/or MSec) -- described by RM_SEND_WINDOW below
		public static readonly SocketOptionName RM_RATE_WINDOW_SIZE = (SocketOptionName)(RM_OPTIONSBASE + 1);

		// set IP multicast outgoing interface
		public static readonly SocketOptionName RM_SET_SEND_IF = (SocketOptionName)(RM_OPTIONSBASE + 7);

		// add IP multicast incoming interface
		public static readonly SocketOptionName RM_ADD_RECEIVE_IF = (SocketOptionName)(RM_OPTIONSBASE + 8);

		// delete IP multicast incoming interface
		public static readonly SocketOptionName RM_DEL_RECEIVE_IF = (SocketOptionName)(RM_OPTIONSBASE + 9);

		// Set the Ttl of the MCast packets -- (ULONG)
		public static readonly SocketOptionName RM_SET_MCAST_TTL = (SocketOptionName)(RM_OPTIONSBASE + 12);

		public static readonly SocketOptionName EnableGigabitOption = (SocketOptionName)1014;

		private readonly Options m_options;
		private readonly PgmSocketType m_pgmSocketType;
		private readonly PgmAddress m_pgmAddress;

		public PgmSocket(Options options, PgmSocketType pgmSocketType, PgmAddress pgmAddress)
		{
			m_options = options;
			m_pgmSocketType = pgmSocketType;
			m_pgmAddress = pgmAddress;
		}

		internal void Init()
		{
			FD = new Socket(AddressFamily.InterNetwork, SocketType.Rdm, PGM_PROTOCOL_TYPE);
			FD.ExclusiveAddressUse = false;
			FD.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		}

		internal void Init(Socket fd)
		{
			FD = fd;

		}
		

		internal void InitOptions()
		{
			// Enable gigabit on the socket
			FD.SetSocketOption(PGM_LEVEL, EnableGigabitOption, BitConverter.GetBytes((uint)1));

			// set the receive buffer size for receiver and listener
			if (m_options.ReceiveBuffer > 0 && m_pgmSocketType == PgmSocketType.Receiver || m_pgmSocketType == PgmSocketType.Listener)
			{
				FD.ReceiveBufferSize = m_options.ReceiveBuffer;
			}

			// set the send buffer for the publisher
			if (m_options.SendBuffer > 0 && m_pgmSocketType == PgmSocketType.Publisher)
			{
				FD.SendBufferSize = m_options.SendBuffer;
			}

			// set the receive interface on the listener and receiver
			if (m_pgmSocketType == PgmSocketType.Listener || m_pgmSocketType == PgmSocketType.Receiver)
			{
				if (m_pgmAddress.InterfaceAddress != null)
				{
					FD.SetSocketOption(PGM_LEVEL, RM_ADD_RECEIVE_IF, m_pgmAddress.InterfaceAddress.GetAddressBytes());
				}
			}
			else if (m_pgmSocketType == PgmSocketType.Publisher)
			{
				// set multicast hops for the publisher
				FD.SetSocketOption(PGM_LEVEL, RM_SET_MCAST_TTL, m_options.MulticastHops);

				// set the publisher send interface
				if (m_pgmAddress.InterfaceAddress != null)
				{
					FD.SetSocketOption(PGM_LEVEL, RM_SET_SEND_IF, m_pgmAddress.InterfaceAddress.GetAddressBytes());
				}

				// instead of using the struct _RM_SEND_WINDOW we are using byte array of size 12 (the size of the original struct and the size of three ints)
				//  typedef struct _RM_SEND_WINDOW {
				//  ULONG RateKbitsPerSec;
				//  ULONG WindowSizeInMSecs;
				//  ULONG WindowSizeInBytes;
				//} RM_SEND_WINDOW;
				byte[] sendWindow = new byte[12];

				// setting the rate of the transmittion in Kilobits per second
				uint rate = (uint)(m_options.Rate);
				Array.Copy(BitConverter.GetBytes(rate), 0, sendWindow, 0, 4);

				// setting the recovery interval
				uint sizeInMS = (uint)(m_options.RecoveryIvl);
				Array.Copy(BitConverter.GetBytes(sizeInMS), 0, sendWindow, 4, 4);

				// we are not setting the size in bytes because it get filled automaticlly, if we want to set it we would just uncomment the following lines
				//uint sizeInBytes = (uint)((rate / 8.0) * sizeInMS);
				//Array.Copy(BitConverter.GetBytes(sizeInBytes), 0, sendWindow, 8, 4);

				FD.SetSocketOption(PGM_LEVEL, RM_RATE_WINDOW_SIZE, sendWindow);
			}
		}

		public Socket FD { get; private set; }
	}
}
