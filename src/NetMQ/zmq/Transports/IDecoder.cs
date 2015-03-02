using JetBrains.Annotations;

namespace NetMQ.zmq.Transports
{
    internal interface IDecoder
    {
        void SetMsgSink([NotNull] IMsgSink msgSink);

        void GetBuffer(out ByteArraySegment data, out int size);

        int ProcessBuffer([NotNull] ByteArraySegment data, int size);

        bool MessageReadySize(int msgSize);

        bool Stalled();
    }
}
