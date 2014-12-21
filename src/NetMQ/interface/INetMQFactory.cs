namespace NetMQ
{
	using NetMQ.zmq;

	public interface INetMQFactory
	{
		INetMQContext CreateContext();

		IPoller CreatePoller();

		IPoller CreatePoller(params ISocketPollable[] sockets);

		IPoller CreatePoller(params NetMQTimer[] timers);

		INetMQMonitor CreateMonitor(INetMQSocket socket, string endpoint);

		INetMQMonitor CreateMonitor(INetMQContext context, INetMQSocket monitoredSocket, string endpoint, SocketEvent eventsToMonitor);

		ISubscriberSocket CreateSubscriberSocket(SocketBase socketHandle);
	}
}
