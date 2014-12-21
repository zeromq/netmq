namespace NetMQ
{
	using System.Text;

	using NetMQ.zmq;

	public interface ISubscriberSocket : INetMQSocket
	{
		void Subscribe(string topic, Encoding encoding);

		void Unsubscribe(string topic, Encoding encoding);
	}
}