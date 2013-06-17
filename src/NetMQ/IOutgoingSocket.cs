namespace NetMQ
{
	public interface IOutgoingSocket
	{
		void Send(byte[] data, int length, bool dontWait = false, bool sendMore = false);
	}
}
