using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public abstract class BaseSocket : IDisposable
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

		internal SocketBase SocketHandle
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

		protected Msg ReceiveInternal(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ZMQ.Recv(m_socketHandle, options);
			
			hasMore = msg.HasMore;

			return msg;
		}

		protected string ReceiveStringInternal(SendRecieveOptions options, out bool hasMore)
		{
			var msg = ReceiveInternal(options, out hasMore);

			return Encoding.ASCII.GetString(msg.Data);
		}

		protected IList<byte[]> ReceiveAllInternal()
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

		protected IList<string> ReceiveAllStringInternal()
		{
			bool hasMore;

			IList<string> messages = new List<string>();

			var msg = ReceiveStringInternal(SendRecieveOptions.None, out hasMore);
			messages.Add(msg);

			while (hasMore)
			{
				msg = ReceiveStringInternal(SendRecieveOptions.None, out hasMore);
				messages.Add(msg);
			}

			return messages;
		}

		protected void SendInternal(byte[] data, int length, SendRecieveOptions options)
		{
			Msg msg = new Msg(data, length, Options.CopyMessages);

			ZMQ.Send(m_socketHandle, msg, options);			
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
				sendRecieveOptions |= SendRecieveOptions.SendMore;
			}

			SendInternal(data, length, sendRecieveOptions);
		}

		protected void SendInternal(string message, bool dontWait, bool sendMore)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);

			SendInternal(data, data.Length, dontWait, sendMore);
		}
		
		internal int GetSocketOption(ZmqSocketOptions socketOptions)
		{
			return ZMQ.GetSocketOption(m_socketHandle, socketOptions);
		}

		internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
		{
			return TimeSpan.FromMilliseconds(ZMQ.GetSocketOption(m_socketHandle, socketOptions));
		}

		internal long GetSocketOptionLong(ZmqSocketOptions socketOptions)
		{
			return (long)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
		}

		internal T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
		{
			return (T)ZMQ.GetSocketOptionX(m_socketHandle, socketOptions);
		}

		internal void SetSocketOption(ZmqSocketOptions socketOptions, int value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
		}

		internal void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, (int)value.TotalMilliseconds);
		}

		internal void SetSocketOption(ZmqSocketOptions socketOptions, object value)
		{
			ZMQ.SetSocketOption(m_socketHandle, socketOptions, value);
		}

		public void Dispose()
		{
			Close();
		}
	}


}
