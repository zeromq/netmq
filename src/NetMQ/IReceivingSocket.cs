namespace NetMQ
{
	public interface IReceivingSocket
	{
		byte[] Receive(bool dontWait, out bool hasMore);
	}
}