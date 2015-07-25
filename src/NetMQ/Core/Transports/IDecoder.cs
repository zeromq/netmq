using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    internal interface IDecoder
    {
        void SetMsgSink([CanBeNull] IMsgSink msgSink);

        void GetBuffer(out ByteArraySegment data, out int size);

        int ProcessBuffer([NotNull] ByteArraySegment data, int size);

        bool MessageReadySize(int msgSize);

        bool Stalled();
    }
}
