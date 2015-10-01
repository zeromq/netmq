using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    /// <summary>
    /// Interface IEncoder mandates SetMsgSource and GetData methods.
    /// </summary>
    internal interface IEncoder
    {
        /// <summary>
        /// Set message producer.
        /// </summary>
        void SetMsgSource([CanBeNull] IMsgSource msgSource);

        /// <summary>
        /// Get a ByteArraySegment of binary data. The data
        /// are filled to a supplied buffer. If no buffer is supplied (data is null)
        /// encoder will provide buffer of its own.
        /// </summary>
        void GetData([NotNull] ref ByteArraySegment data, ref int size);

        void GetData([NotNull] ref ByteArraySegment data, ref int size, ref int offset);
    }
}
