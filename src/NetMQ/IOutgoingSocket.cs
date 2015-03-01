using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// Interface IOutgoingSocket mandates a Send( Msg, SendReceiveOptions ) method.
    /// </summary>
    public interface IOutgoingSocket
    {
        /// <summary>
        /// Send the given Msg out upon this socket.
        /// The message content is in the form of a byte-array that Msg contains.
        /// </summary>
        /// <param name="msg">the Msg struct that contains the data and the options for this transmission</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore bits</param>
        void Send(ref Msg msg, SendReceiveOptions options);
    }
}
