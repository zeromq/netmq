namespace NetMQ.zmq
{
	public interface IMsgSink
	{
		//  Delivers a message. Returns true if successful; false otherwise.
		//  The function takes ownership of the passed message.
		bool PushMsg (Msg msg);
	}
}
