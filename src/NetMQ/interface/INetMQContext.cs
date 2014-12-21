namespace NetMQ
{
	using System;
	using NetMQ.Monitoring;
	using NetMQ.Sockets;
	using NetMQ.zmq;

	public interface INetMQContext : IDisposable
	{
		INetMQFactory Factory { get; }

		int MaxSockets { get; set; }

		int ThreadPoolSize { get; set; }

		IDealerSocket CreateDealerSocket();

		INetMQMonitor CreateMonitorSocket(string endpoint);

		IPairSocket CreatePairSocket();

		IPublisherSocket CreatePublisherSocket();

		IPullSocket CreatePullSocket();

		IPushSocket CreatePushSocket();

		IRequestSocket CreateRequestSocket();

		IResponseSocket CreateResponseSocket();

		IRouterSocket CreateRouterSocket();

		INetMQSocket CreateSocket(ZmqSocketType socketType);

		IStreamSocket CreateStreamSocket();

		ISubscriberSocket CreateSubscriberSocket();

		XPublisherSocket CreateXPublisherSocket();

		XSubscriberSocket CreateXSubscriberSocket();

		void Terminate();
	}
}