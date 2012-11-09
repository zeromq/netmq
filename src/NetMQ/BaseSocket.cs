using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public abstract class BaseSocket
	{
		readonly SocketBase m_socketHandle;

		protected BaseSocket(SocketBase socketHandle)
		{
			m_socketHandle = socketHandle;
            Options = new SocketOptions(this);
		}

        public SocketOptions Options { get; private set; }

        internal SocketBase SocketHandle
        {
            get
            {
                return m_socketHandle;
            }
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

        public void Close()
        {
            ZMQ.Close(m_socketHandle);
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
				sendRecieveOptions |= SendRecieveOptions.SendMore;
			}

			SendInternal(data, length, sendRecieveOptions);
		}

		protected void SendInternal(string message, bool dontWait, bool sendMore)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);

			SendInternal(data, data.Length, dontWait, sendMore);
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
	}


}
