using System;
using NetMQ.zmq;

namespace NetMQ
{
	public interface INetMQSocket : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
	{
		bool HasIn { get; }

		bool HasOut { get; }

		[Obsolete]
		bool IgnoreErrors { get; set; }

		SocketOptions Options { get; }

        SocketBase SocketHandle { get; }

		event EventHandler<NetMQSocketEventArgs> ReceiveReady;

		event EventHandler<NetMQSocketEventArgs> SendReady;

		event EventHandler<NetMQSocketEventArgs> EventsChanged;

		int Errors { get; set; }

		void Bind(string address);

		int BindRandomPort(string address);

		void Close();

		void Connect(string address);

		void Disconnect(string address);

		void Monitor(string endpoint, SocketEvent events = SocketEvent.All);

		void Poll();

		PollEvents Poll(PollEvents pollEvents, TimeSpan timeout);

		bool Poll(TimeSpan timeout);

		PollEvents GetPollEvents();

		void InvokeEvents(object sender, PollEvents events);

		[Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
		void Subscribe(byte[] topic);

		[Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
		void Subscribe(string topic);

		void Unbind(string address);

		[Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
		void Unsubscribe(byte[] topic);

		[Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
		void Unsubscribe(string topic);
	}
}
