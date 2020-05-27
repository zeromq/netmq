namespace NetMQ.Core.Transports
{
    internal enum DecodeResult
    {
        Error,
        Processing,
        MessageReady
    }
    
    internal interface IDecoder
    {
        void GetBuffer(out ByteArraySegment data, out int size);

        DecodeResult Decode(ByteArraySegment data, int size, out int processed);

        PushMsgResult PushMsg(ProcessMsgDelegate sink);
    }
}
