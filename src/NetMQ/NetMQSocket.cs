using System;
using System.Collections.Generic;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public abstract class NetMQSocket : IOutgoingSocket, IReceivingSocket, IDisposable
	{
		readonly SocketBase m_socketHandle;
		private bool m_isClosed = false;
		private NetMQSocketEventArgs m_socketEventArgs;

		private EventHandler<NetMQSocketEventArgs> m_receiveReady;

		private EventHandler<NetMQSocketEventArgs> m_sendReady;

		protected NetMQSocket(SocketBase socketHandle)
		{
			m_socketHandle = socketHandle;
			Options = new SocketOptions(this);
			m_socketEventArgs = new NetMQSocketEventArgs(this);

			IgnoreErrors = false;
			Errors = 0;
		}

		/// <summary>
		/// Occurs when at least one message may be received from the socket without blocking.
		/// </summary>
		public event EventHandler<NetMQSocketEventArgs> ReceiveReady
		{
			add
			{
				m_receiveReady += value;
				InvokeEventsChanged();
			}
			remove
			{
				m_receiveReady -= value;
				InvokeEventsChanged();
			}
		}

		/// <summary>
		/// Occurs when at least one message may be sent via the socket without blocking.
		/// </summary>
		public event EventHandler<NetMQSocketEventArgs> SendReady
		{
			add
			{
				m_sendReady += value;
				InvokeEventsChanged();
			}
			remove
			{
				m_sendReady -= value;
				InvokeEventsChanged();
			}
		}

		public bool IgnoreErrors { get; set; }

		internal event EventHandler<NetMQSocketEventArgs> EventsChanged;

		internal int Errors { get; set; }


		private void InvokeEventsChanged()
		{
			var temp = EventsChanged;

			if (temp != null)
			{
				m_socketEventArgs.Init(PollEvents.None);
				temp(this, m_socketEventArgs);
			}
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

		/// <summary> Wait until message is ready to be received from the socket. </summary>
		public void Poll()
		{
			Poll(TimeSpan.FromMilliseconds(-1));
		}

		/// <summary>
		/// Wait until message is ready to be received from the socket or until timeout is reached
		/// </summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool Poll(TimeSpan timeout)
		{
			PollEvents events = GetPollEvents();

			PollItem item = new PollItem(m_socketHandle, events);

			PollItem[] items = new PollItem[] { item };

			ZMQ.Poll(items, (int)timeout.TotalMilliseconds);

			if (item.ResultEvent.HasFlag(PollEvents.PollError) && !IgnoreErrors)
			{
				Errors++;

				if (Errors > 1)
				{
					throw new ErrorPollingException("Error while polling", this);
				}
			}
			else
			{
				Errors = 0;
			}

			InvokeEvents(this, item.ResultEvent);

			return items[0].ResultEvent != PollEvents.None;
		}

		internal PollEvents GetPollEvents()
		{
			PollEvents events = PollEvents.PollError;

			if (m_sendReady != null)
			{
				events |= PollEvents.PollOut;
			}

			if (m_receiveReady != null)
			{
				events |= PollEvents.PollIn;
			}

			return events;
		}

		internal void InvokeEvents(object sender, PollEvents events)
		{
			if (!m_isClosed)
			{
				m_socketEventArgs.Init(events);

				if (events.HasFlag(PollEvents.PollIn))
				{
					var temp = m_receiveReady;
					if (temp != null)
					{
						temp(sender, m_socketEventArgs);
					}
				}

				if (events.HasFlag(PollEvents.PollOut))
				{
					var temp = m_sendReady;
					if (temp != null)
					{
						temp(sender, m_socketEventArgs);
					}
				}
			}
		}

		protected internal virtual Msg ReceiveInternal(SendReceiveOptions options, out bool hasMore)
		{
			var msg = ZMQ.Recv(m_socketHandle, options);

			hasMore = msg.HasMore;

			return msg;
		}

		public byte[] Receive(bool dontWait, out bool hasMore)
		{
			return ReceiveInternal(dontWait ? SendReceiveOptions.DontWait : SendReceiveOptions.None, out hasMore).Data;
		}

		public void SendMessage(NetMQMessage message, bool dontWait = false)
		{
			for (int i = 0; i < message.FrameCount - 1; i++)
			{
				Send(message[i].Buffer, message[i].MessageSize, dontWait, true);
			}

			Send(message.Last.Buffer, message.Last.MessageSize, dontWait);
		}

		public virtual void Send(byte[] data, int length, SendReceiveOptions options)
		{
			Msg msg = new Msg(data, length, Options.CopyMessages);

			ZMQ.Send(m_socketHandle, msg, options);
		}

		public void Send(byte[] data, int length, bool dontWait = false, bool sendMore = false)
		{
			SendReceiveOptions sendReceiveOptions = SendReceiveOptions.None;

			if (dontWait)
			{
				sendReceiveOptions |= SendReceiveOptions.DontWait;
			}

			if (sendMore)
			{
				sendReceiveOptions |= SendReceiveOptions.SendMore;
			}

			Send(data, length, sendReceiveOptions);
		}

        [Obsolete ("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

		public void Monitor(string endpoint, SocketEvent events = SocketEvent.All)
		{
			if (endpoint == null)
			{
				throw new ArgumentNullException("endpoint");
			}

			if (endpoint == string.Empty)
			{
				throw new ArgumentException("Unable to publish socket events to an empty endpoint.", "endpoint");
			}

			ZMQ.SocketMonitor(SocketHandle, endpoint, events);
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
