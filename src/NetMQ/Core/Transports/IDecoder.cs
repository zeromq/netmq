using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    internal enum DecodeResult
    {
        Error,
        Processing,
        MessageReady
    }

    internal delegate bool MsgSink(ref Msg msg); 
    
    internal interface IDecoder
    {
        void GetBuffer(out ByteArraySegment data, out int size);

        DecodeResult Decode ([NotNull] ByteArraySegment data, int size, out int processed);
        bool GetMsg(MsgSink sink);
    }
}
