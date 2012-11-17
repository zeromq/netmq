namespace NetMQ.zmq
{
	public interface IEncoder
	{
		//  Set message producer.
		void SetMsgSource (IMsgSource msgSource);

		//  The function returns a batch of binary data. The data
		//  are filled to a supplied buffer. If no buffer is supplied (data_
		//  is nullL) encoder will provide buffer of its own.
		void GetData(ref ByteArraySegment data, ref int size);

		void GetData(ref ByteArraySegment data, ref int size, ref int offset);
	}
}
