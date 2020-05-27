using System;

namespace NetMQ.Core.Transports
{
    /// <summary>
    /// Interface IEncoder mandates SetMsgSource and GetData methods.
    /// </summary>
    internal interface IEncoder : IDisposable
    {
        /// <summary>
        /// Get a ByteArraySegment of binary data. The data
        /// are filled to a supplied buffer. If no buffer is supplied (data is null)
        /// encoder will provide buffer of its own.
        /// </summary>
        /// <returns>Returns data size or 0 when a new message is required</returns>
        int Encode(ref ByteArraySegment? data, int size);
        
        /// <summary>
        /// Load a new message into encoder.
        /// </summary>
        /// <param name="msg"></param>
        void LoadMsg(ref Msg msg);
    }
}
