using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Request socket
	/// </summary>
	class RequestSocket : BaseSocket, IRequestSocket
	{
		public RequestSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}


		
	}
}
