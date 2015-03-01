using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// Interface IReceivingSocket mandates a Receive( Msg, SendReceiveOptions ) method.
    /// </summary>
    public interface IReceivingSocket
    {
        /// <summary>
        /// Read an available message into the given Msg. The message content is in the form of a byte-array
        /// which higher-level methods may convert into a string.
        /// </summary>
        /// <param name="msg">a Msg struct to receive the message data into</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore bits</param>
        void Receive(ref Msg msg, SendReceiveOptions options);
    }
}