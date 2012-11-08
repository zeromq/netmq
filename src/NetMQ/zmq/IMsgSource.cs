namespace NetMQ.zmq
{
	public interface IMsgSource
	{
		//  Fetch a message. Returns a Msg instance if successful; null otherwise.
		Msg PullMsg ();
	}
}
