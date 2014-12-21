namespace NetMQ
{
	using System.Text;

	using NetMQ.zmq;

	public interface ISubscriberSocket : INetMQSocket
	{
		void Send(ref Msg msg, SendReceiveOptions options);

		void Subscribe(byte[] topic);

		void Subscribe(string topic);

		void Subscribe(string topic, Encoding encoding);

		void Unsubscribe(byte[] topic);

		void Unsubscribe(string topic);

		void Unsubscribe(string topic, Encoding encoding);
	}
}
