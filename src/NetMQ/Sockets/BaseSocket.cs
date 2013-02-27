using System;
using System.Collections.Generic;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	public abstract class BaseSocket : ISocket, ISocketExtended
	{
		readonly SocketBase m_socketHandle;
		private bool m_isClosed = false;

		protected BaseSocket(SocketBase socketHandle)
		{
			m_socketHandle = socketHandle;
			Options = new SocketOptions(this);
		}

		/// <summary>
		/// Set the options of the socket
		/// </summary>
		public SocketOptions Options { get; private set; }

		public SocketBase SocketHandle
		{
			get
			{
				return m_socketHandle;
			}
		}

		/// <summary>
		/// Bind the socket to an address
		/// </summary>
		/// <param name="address">The address of the socket</param>
		public void Bind(string address)
		{
			ZMQ.Bind(m_socketHandle, address);
		}

		/// <summary>
		/// Connect the socket to an address
		/// </summary>
		/// <param name="address">Address to connect to</param>
		public void Connect(string address)
		{
			ZMQ.Connect(m_socketHandle, address);
		}

		/// <summary>
		/// Disconnect the socket from specific address
		/// </summary>
		/// <param name="address">The address to disconnect from</param>
		public void Disconnect(string address)
		{
			ZMQ.Disconnect(m_socketHandle, address);
		}

		/// <summary>
		/// Unbind the socket from specific address
		/// </summary>
		/// <param name="address">The address to unbind from</param>
		public void Unbind(string address)
		{
			ZMQ.Unbind(m_socketHandle, address);
		}

		/// <summary>
		/// Close the socket
		/// </summary>
		public void Close()
		{
			if (!m_isClosed)
			{
				m_isClosed = true;
				ZMQ.Close(m_socketHandle);
			}
		}

		/// <summary>
		/// Wait until message is ready to be received from the socket or until timeout is reached
		/// </summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool Poll(TimeSpan timeout, PollEvents events)
		{
			PollItem[] items = new PollItem[1];

			items[0] = new PollItem(m_socketHandle, events);

			ZMQ.Poll(items, (int)timeout.TotalMilliseconds);

			return (items[0].ResultEvent != PollEvents.None);

		}

		protected internal virtual Msg ReceiveInternal(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ZMQ.Recv(m_socketHandle, options);

			hasMore = msg.HasMore;

			return msg;
		}

		public byte[] Receive(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ReceiveInternal(options, out hasMore);

			return msg.Data;
		}

		public byte[] Receive(out bool hasMore)
		{
			var msg = ReceiveInternal(SendRecieveOptions.None, out hasMore);

			return msg.Data;
		}

		public byte[] Receive(SendRecieveOptions options)
		{
			bool hasMore;

			var msg = ReceiveInternal(options, out hasMore);

			return msg.Data;
		}

		public byte[] Receive()
		{
			bool hasMore;

			var msg = ReceiveInternal(SendRecieveOptions.None, out hasMore);

			return msg.Data;
		}

		public byte[] Receive(bool dontWait, out bool hasMore)
		{
			return Receive(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out hasMore);
		}

		public string ReceiveString(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ReceiveInternal(options, out hasMore);

			return Encoding.ASCII.GetString(msg.Data);
		}

		public string ReceiveString(SendRecieveOptions options)
		{
			bool more;

			return ReceiveString(options, out more);
		}

		public string ReceiveString(out bool more)
		{
			return ReceiveString(SendRecieveOptions.None, out more);
		}

		public string ReceiveString()
		{
			return ReceiveString(SendRecieveOptions.None);
		}

		public string ReceiveString(bool dontWait, out bool hasMore)
		{
			return ReceiveString(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out hasMore);
		}

		public IList<byte[]> ReceiveAll()
		{
			bool hasMore;

			IList<byte[]> messages = new List<byte[]>();

			Msg msg = ReceiveInternal(SendRecieveOptions.None, out hasMore);
			messages.Add(msg.Data);

			while (hasMore)
			{
				msg = ReceiveInternal(SendRecieveOptions.None, out hasMore);
				messages.Add(msg.Data);
			}

			return messages;
		}

		public IList<string> ReceiveAllString()
		{
			bool hasMore;

			IList<string> messages = new List<string>();

			var msg = ReceiveString(SendRecieveOptions.None, out hasMore);
			messages.Add(msg);

			while (hasMore)
			{
				msg = ReceiveString(SendRecieveOptions.None, out hasMore);
				messages.Add(msg);
			}

			return messages;
		}

		public virtual void Send(byte[] data, int length, SendRecieveOptions options)
		{
			Msg msg = new Msg(data, length, Options.CopyMessages);

			ZMQ.Send(m_socketHandle, msg, options);
		}

		public void Send(byte[] data)
		{
			Send(data, data.Length, false, false);
		}

		public void Send(byte[] data, int length)
		{
			Send(data, length, false, false);
		}

		public void Send(byte[] data, int length, bool dontWait, bool sendMore)
		{
			SendRecieveOptions sendRecieveOptions = SendRecieveOptions.None;

			if (dontWait)
			{
				sendRecieveOptions |= SendRecieveOptions.DontWait;
			}

			if (sendMore)
			{
				sendRecieveOptions |= SendRecieveOptions.SendMore;
			}

			Send(data, length, sendRecieveOptions);
		}

		public void Send(string message, bool dontWait, bool sendMore)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);

			Send(data, data.Length, dontWait, sendMore);
		}

		public void Send(string message)
		{
			Send(message, false, false);
		}

		public IOutgoingSocket SendMore(string message)
		{
			Send(message, false, true);

			return (IOutgoingSocket) this;
		}

		public IOutgoingSocket SendMore(string message, bool dontWait)
		{
			Send(message, dontWait, true);

			return (IOutgoingSocket)this;
		}

		public IOutgoingSocket SendMore(byte[] data)
		{
			Send(data, data.Length, false, true);

			return (IOutgoingSocket)this;
		}

		public IOutgoingSocket SendMore(byte[] data, bool dontWait)
		{
			Send(data, data.Length, dontWait, true);

			return (IOutgoingSocket)this;
		}

		public IOutgoingSocket SendMore(byte[] data, int length)
		{
			Send(data, length, false, true);

			return (IOutgoingSocket)this;
		}

		public IOutgoingSocket SendMore(byte[] data, int length, bool dontWait)
		{
			Send(data, length, dontWait, true);

			return (IOutgoingSocket)this;
		}


		public int GetSocketOption(ZmqSocketOptions socketOptions)
		{
			return ZMQ.GetSocketOption(m_socketHandle, socketOptions);
		}

		public TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
		{
			return TimeSpan.FromMilliseconds(ZMQ.GetSocketOption(m_socketHandle, socketOptions));
		}

		public long GetSocketOptionLong(ZmqSocketOptions socketOptions)
		{
			return (long)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
		}

		public T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
		{
			return (T)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
		}

		public void SetSocketOption(ZmqSocketOptions socketOptions, int value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
		}

		public void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, (int)value.TotalMilliseconds);
		}

		public void SetSocketOption(ZmqSocketOptions socketOptions, object value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
		}

		public void Dispose()
		{
			Close();
		}
	}


}
