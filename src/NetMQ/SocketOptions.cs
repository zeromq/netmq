using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ
{
	public class SocketOptions
	{
		NetMQSocket m_socket;

		public SocketOptions(NetMQSocket socket)
		{
			m_socket = socket;
		}

		public long Affinity
		{
			get { return m_socket.GetSocketOptionLong(ZmqSocketOptions.Affinity); }
			set
			{
				m_socket.SetSocketOption(ZmqSocketOptions.Affinity, value);
			}
		}

		public bool CopyMessages { get; set; }

		public byte[] Identity
		{
			get { return m_socket.GetSocketOptionX<byte[]>(ZmqSocketOptions.Identity); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.Identity, value); }
		}

		public int MulticastRate
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.Rate); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.Rate, value); }
		}

		public TimeSpan MulticastRecoveryInterval
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
		}

		public int SendBuffer
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.SendBuffer); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.SendBuffer, value); }
		}
		public int ReceivevBuffer
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.ReceivevBuffer); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.ReceivevBuffer, value); }
		}

		public bool ReceiveMore
		{
			get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.ReceiveMore); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.ReceiveMore, value); }
		}

		public TimeSpan Linger
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.Linger); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.Linger, value); }
		}

		public TimeSpan ReconnectInterval
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
		}

		public TimeSpan ReconnectIntervalMax
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
		}

		public int Backlog
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.Backlog); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.Backlog, value); }
		}

		public int MaxMsgSize
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.Maxmsgsize); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.Maxmsgsize, value); }
		}

		public int SendHighWatermark
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.SendHighWatermark); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.SendHighWatermark, value); }
		}

		public int ReceiveHighWatermark
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.ReceivevHighWatermark); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.ReceivevHighWatermark, value); }
		}

		public int MulticastHops
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.MulticastHops); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.MulticastHops, value); }
		}

		public TimeSpan ReceiveTimeout
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReceiveTimeout); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReceiveTimeout, value); }
		}

		public TimeSpan SendTimeout
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.SendTimeout); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.SendTimeout, value); }
		}

		public bool IPv4Only
		{
			get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.IPv4Only); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.IPv4Only, value); }
		}

		public string GetLastEndpoint { get { return m_socket.GetSocketOptionX<string>(ZmqSocketOptions.LastEndpoint); } }

		public bool RouterMandatory
		{
			get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.RouterMandatory); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.RouterMandatory, value); }
		}

		public bool TcpKeepalive
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.TcpKeepalive) == 1; }
			set { m_socket.SetSocketOption(ZmqSocketOptions.TcpKeepalive, value ? 1 : 0); }
		}

		public int TcpKeepaliveCnt
		{
			get { return m_socket.GetSocketOption(ZmqSocketOptions.TcpKeepaliveCnt); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.TcpKeepaliveCnt, value); }
		}

		public TimeSpan TcpKeepaliveIdle
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIdle); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIdle, value); }
		}

		public TimeSpan TcpKeepaliveInterval
		{
			get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIntvl); }
			set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIntvl, value); }
		}

		public string TcpAcceptFilter
		{
			get { return m_socket.GetSocketOptionX<string>(ZmqSocketOptions.TcpAcceptFilter); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.TcpKeepaliveIntvl, value); }
		}

		public bool DelayAttachOnConnect
		{
			get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.DelayAttachOnConnect); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.DelayAttachOnConnect, value); }
		}

		public bool XPubVerbose
		{
			get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.XpubVerbose); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.XpubVerbose, value); }
		}

		public Endianness Endian
		{
			get { return m_socket.GetSocketOptionX<Endianness>(ZmqSocketOptions.Endian); }
			set { m_socket.SetSocketOption(ZmqSocketOptions.Endian, value); }
		}

	}
}
