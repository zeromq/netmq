using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using zmq;

namespace NetMQ
{
	public abstract class BaseSocket
	{
		readonly SocketBase m_socketHandle;

		protected BaseSocket(SocketBase socketHandle)
		{
			m_socketHandle = socketHandle;
		}


		public void Bind(string address)
		{
			if (!ZMQ.Bind(m_socketHandle, address))
			{
				throw new NetMQException("Bind socket failed", ZError.ErrorNumber);
			}
		}

		public void Connect(string address)
		{
			if (!ZMQ.Connect(m_socketHandle, address))
			{
				throw new NetMQException("Connect socket failed", ZError.ErrorNumber);
			}
		}

		public void Disconnect(string address)
		{
			if (!ZMQ.Disconnect(m_socketHandle, address))
			{
				throw new NetMQException("Disconnect socket failed", ZError.ErrorNumber);
			}
		}

		public void Unbind(string address)
		{
			if (!ZMQ.Unbind(m_socketHandle, address))
			{
				throw new NetMQException("Unbind socket failed", ZError.ErrorNumber);
			}
		}

		protected Msg ReceiveInternal(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ZMQ.Recv(m_socketHandle, options);

			if (msg == null)
			{
				HandleError("Receive failed");
			}

			hasMore = msg.HasMore;

			return msg;
		}

		protected string ReceiveStringInternal(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ReceiveInternal(options, out hasMore);		

			return Encoding.ASCII.GetString(msg.Data);
		}

		protected void SendInternal(byte[] data, int length, SendRecieveOptions options)
		{
			Msg msg = new Msg(data, length, CopyMessages);

			int bytesSend = ZMQ.Send(m_socketHandle, msg, options);

			if (bytesSend < 0)
			{
				HandleError("Sending message failed");
			}
		}

		protected void SendInternal(byte[] data, int length, bool dontWait, bool sendMore)
		{
			SendRecieveOptions sendRecieveOptions = SendRecieveOptions.None;

			if (dontWait)
			{
				sendRecieveOptions |= SendRecieveOptions.DontWait;
			}

			if (sendMore)
			{
				sendRecieveOptions |= SendRecieveOptions.DontWait;
			}

			SendInternal(data, length, sendRecieveOptions);
		}

		protected void SendInternal(string message, bool dontWait, bool sendMore)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);

			SendInternal(data, data.Length, dontWait, sendMore);
		}

		public long Affinity
		{
			get { return GetSocketOptionLong(ZmqSocketOptions.Affinity); }
			set
			{
				SetSocketOption(ZmqSocketOptions.Affinity, value);
			}
		}

		private void HandleError(string message)
		{
			if (ZError.ErrorNumber != ErrorNumber.ETERM)
			{
				if (ZError.ErrorNumber == ErrorNumber.EAGAIN)
				{
					throw new TryAgainException("Cannot complete without block, please try again later", ZError.ErrorNumber);
				}
				else
				{
					throw new NetMQException(message, ZError.ErrorNumber);
				}
			}
		}

		public bool CopyMessages { get; set; }

		public byte[] Identity
		{
			get { return GetSocketOptionX<byte[]>(ZmqSocketOptions.Identity); }
			set { SetSocketOption(ZmqSocketOptions.Identity, value); }
		}

		public int MulticastRate
		{
			get { return GetSocketOption(ZmqSocketOptions.Rate); }
			set { SetSocketOption(ZmqSocketOptions.Rate, value); }
		}

		public int MulticastRecoveryInterval
		{
			get { return GetSocketOption(ZmqSocketOptions.ReconnectIvl); }
			set { SetSocketOption(ZmqSocketOptions.ReconnectIvl, value); }
		}

		public int SendBuffer
		{
			get { return GetSocketOption(ZmqSocketOptions.SendBuffer); }
			set { SetSocketOption(ZmqSocketOptions.SendBuffer, value); }
		}
		public int ReceivevBuffer
		{
			get { return GetSocketOption(ZmqSocketOptions.ReceivevBuffer); }
			set { SetSocketOption(ZmqSocketOptions.ReceivevBuffer, value); }
		}

		public bool ReceiveMore
		{
			get { return GetSocketOptionX<bool>(ZmqSocketOptions.ReceiveMore); }
			set { SetSocketOption(ZmqSocketOptions.ReceiveMore, value); }
		}

		public TimeSpan Linger
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.Linger); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.Linger, value); }
		}

		public TimeSpan ReconnectInterval
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
		}

		public TimeSpan ReconnectIntervalMax
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
		}

		public int Backlog
		{
			get { return GetSocketOption(ZmqSocketOptions.Backlog); }
			set { SetSocketOption(ZmqSocketOptions.Backlog, value); }
		}

		public int MaxMsgSize
		{
			get { return GetSocketOption(ZmqSocketOptions.Maxmsgsize); }
			set { SetSocketOption(ZmqSocketOptions.Maxmsgsize, value); }
		}

		public int SendHighWatermark
		{
			get { return GetSocketOption(ZmqSocketOptions.SendHighWatermark); }
			set { SetSocketOption(ZmqSocketOptions.SendHighWatermark, value); }
		}

		public int ReceivevHighWatermark
		{
			get { return GetSocketOption(ZmqSocketOptions.ReceivevHighWatermark); }
			set { SetSocketOption(ZmqSocketOptions.ReceivevHighWatermark, value); }
		}

		public int MulticastHops
		{
			get { return GetSocketOption(ZmqSocketOptions.MulticastHops); }
			set { SetSocketOption(ZmqSocketOptions.MulticastHops, value); }
		}

		public TimeSpan ReceiveTimeout
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.ReceiveTimeout); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.ReceiveTimeout, value); }
		}

		public TimeSpan SendTimeout
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.SendTimeout); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.SendTimeout, value); }
		}

		public bool IPv4Only
		{
			get { return GetSocketOptionX<bool>(ZmqSocketOptions.IPv4Only); }
			set { SetSocketOption(ZmqSocketOptions.IPv4Only, value); }
		}

		public string GetLastEndpoint { get { return GetSocketOptionX<string>(ZmqSocketOptions.LastEndpoint); } }

		public bool RouterMandatory
		{
			get { return GetSocketOptionX<bool>(ZmqSocketOptions.RouterMandatory); }
			set { SetSocketOption(ZmqSocketOptions.RouterMandatory, value); }
		}

		public bool TcpKeepalive
		{
			get { return GetSocketOptionX<bool>(ZmqSocketOptions.TcpKeepalive); }
			set { SetSocketOption(ZmqSocketOptions.TcpKeepalive, value); }
		}

		public int TcpKeepaliveCnt
		{
			get { return GetSocketOption(ZmqSocketOptions.TcpKeepaliveCnt); }
			set { SetSocketOption(ZmqSocketOptions.Rate, value); }
		}

		public TimeSpan TcpKeepaliveIdle
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIdle); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.Rate, value); }
		}

		public TimeSpan TcpKeepaliveInterval
		{
			get { return GetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIntvl); }
			set { SetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIntvl, value); }
		}

		public string TcpAcceptFilter
		{
			get { return GetSocketOptionX<string>(ZmqSocketOptions.TcpAcceptFilter); }
			set { SetSocketOption(ZmqSocketOptions.TcpKeepaliveIntvl, value); }
		}

		public bool DelayAttachOnConnect
		{
			get { return GetSocketOptionX<bool>(ZmqSocketOptions.DelayAttachOnConnect); }
			set { SetSocketOption(ZmqSocketOptions.DelayAttachOnConnect, value); }
		}

		protected int GetSocketOption(ZmqSocketOptions socketOptions)
		{
			return ZMQ.GetSocketOption(m_socketHandle, socketOptions);
		}

		protected TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
		{
			return TimeSpan.FromMilliseconds(ZMQ.GetSocketOption(m_socketHandle, socketOptions));
		}

		protected long GetSocketOptionLong(ZmqSocketOptions socketOptions)
		{
			return (long)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
		}

		protected T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
		{
			return (T)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
		}

		protected void SetSocketOption(ZmqSocketOptions socketOptions, int value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
		}

		protected void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, (int)value.TotalMilliseconds);
		}

		protected void SetSocketOption(ZmqSocketOptions socketOptions, object value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
		}
	}


}
