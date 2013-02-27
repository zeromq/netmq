using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Router socket, the first message is always the identity of the sender
	/// </summary>
	class RouterSocket : BaseSocket, IRouterSocket
	{
		public RouterSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}	
	}
}
