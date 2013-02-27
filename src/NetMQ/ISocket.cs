using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public interface ISocket : IDisposable
	{
		SocketOptions Options { get;  }
		
		void Bind(string address);

		void Connect(string address);

		void Disconnect(string address);

		void Unbind(string address);

		void Close();

		bool Poll(TimeSpan timeout, PollEvents events);

		SocketBase SocketHandle { get; }
	}

	public interface ISocketExtended : ISocket
	{
		int GetSocketOption(ZmqSocketOptions socketOptions);

		TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions);

		long GetSocketOptionLong(ZmqSocketOptions socketOptions);

		T GetSocketOptionX<T>(ZmqSocketOptions socketOptions);

		void SetSocketOption(ZmqSocketOptions socketOptions, int value);

		void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value);

		void SetSocketOption(ZmqSocketOptions socketOptions, object value);
	}

	public interface IOutgoingSocket : ISocket
	{
		void Send(byte[] data, int length, SendRecieveOptions options);

		void Send(byte[] data);

		void Send(byte[] data, int length);

		void Send(byte[] data, int length, bool dontWait, bool sendMore);

		void Send(string message, bool dontWait, bool sendMore);

		void Send(string message);

		IOutgoingSocket SendMore(string message);

		IOutgoingSocket SendMore(string message, bool dontWait);

		IOutgoingSocket SendMore(byte[] data);

		IOutgoingSocket SendMore(byte[] data, bool dontWait);

		IOutgoingSocket SendMore(byte[] data, int length);

		IOutgoingSocket SendMore(byte[] data, int length, bool dontWait);
	}

	public interface IIncomingSocket :ISocket
	{
		byte[] Receive(SendRecieveOptions options, out bool hasMore);

		byte[] Receive(SendRecieveOptions options);

		byte[] Receive(bool dontWait, out bool hasMore);

		byte[] Receive(out bool hasMore);

		byte[] Receive();

		string ReceiveString(SendRecieveOptions options, out bool hasMore);

		string ReceiveString(SendRecieveOptions options);

		string ReceiveString(bool dontWait, out bool hasMore);

		string ReceiveString(out bool hasMore);

		string ReceiveString();

		IList<byte[]> ReceiveAll();

		IList<string> ReceiveAllString();
	}

	public interface IDuplexSocket : ISocket, IOutgoingSocket, IIncomingSocket
	{
		
	}

	public interface IDealerSocket : IDuplexSocket
	{
		
	}

	public interface IRequestSocket : IDuplexSocket
	{

	}

	public interface IResponseSocket : IDuplexSocket
	{

	}

	public interface IPairSocket : IDuplexSocket
	{
		
	}

	public interface IRouterSocket : IDuplexSocket
	{

	}

	public interface IPullSocket : ISocket, IIncomingSocket
	{
		
	}

	public interface IPushSocket : ISocket, IOutgoingSocket
	{
		
	}

	public interface IPublisherSocket : ISocket, IOutgoingSocket
	{

	}

	public interface IXPublisherSocket : IPublisherSocket, IIncomingSocket
	{

	}

	public interface ISubscriberSocket : ISocket, IIncomingSocket
	{
		void Subscribe(string topic);

		void Subscribe(byte[] topic);

		void Unsubscribe(string topic);
		
		void Unsubscribe(byte[] topic);
	}

	public interface IXSubscriberSocket: ISubscriberSocket, IOutgoingSocket
	{
		
	}

}
