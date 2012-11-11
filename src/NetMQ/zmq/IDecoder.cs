namespace NetMQ.zmq
{
	public interface IDecoder
	{
		void SetMsgSink (IMsgSink msgSink);

		void GetBuffer(ref ByteArraySegment data, ref int size);

		int ProcessBuffer(ByteArraySegment data, int size);
    
		bool Stalled ();
	}
}
