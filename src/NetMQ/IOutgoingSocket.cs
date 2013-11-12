// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh


namespace NetMQ
{
	public interface IOutgoingSocket
	{
#if !PRE_4
        /// <summary>
        /// Send the given byte-array of data out upon this socket.
        /// </summary>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to send</param>
        /// <param name="dontWait">if true: return from this method-call immediately without waiting for it (optional: defaults to false)</param>
        /// <param name="sendMore">thig indicates whether there is more data to send (optional: defaults to false)</param>
		void Send(byte[] data, int length, bool dontWait = false, bool sendMore = false);
#else
        /// <summary>
        /// Send the given byte-array of data out upon this socket - with dontWait and sendMore both defaulting to false.
        /// </summary>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to send</param>
		void Send(byte[] data, int length);
		void Send(byte[] data, int length, bool dontWait, bool sendMore);
#endif
	}
}
