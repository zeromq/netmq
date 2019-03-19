using NetMQ.Core;
using NetMQ.Core.Patterns;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Peer socket, the first message is always the identity of the sender
    /// </summary>
    public class PeerSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new PeerSocket and attach socket to zero or more endpoints.
        /// </summary>
        /// <param name="connectionString">List of NetMQ endpoints, separated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is connect (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new PeerSocket(">tcp://127.0.0.1:5555,>tcp://127.0.0.1:55556");</code></example>
        public PeerSocket(string connectionString = null) : base(ZmqSocketType.Peer, connectionString, DefaultAction.Connect)
        {
        }

        /// <summary>
        /// Create a new PeerSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal PeerSocket(SocketBase socketHandle) : base(socketHandle)
        {
        }

        /// <summary>
        /// Connect the peer socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to connect this socket to</param>
        /// <returns>The peer allocated routing id</returns>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="NetMQException">No IO thread was found.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        public byte[] ConnectPeer(string address)
        {
            Connect(address);
            return GetSocketOptionX<byte[]>(ZmqSocketOption.LastPeerRoutingId);
        }
    }
}
