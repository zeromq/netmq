namespace NetMQ.zmq.Transports
{
    internal interface IDecoder
    {
        void SetMsgSink(IMsgSink msgSink);

        void GetBuffer(out ByteArraySegment data, out int size);

        int ProcessBuffer(ByteArraySegment data, int size);

        bool MessageReadySize(int msgSize);

        bool Stalled();
    }
}
