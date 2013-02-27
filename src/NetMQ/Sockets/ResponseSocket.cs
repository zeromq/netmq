using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Response socket
	/// </summary>
	class ResponseSocket : BaseSocket, IResponseSocket
	{
		public ResponseSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}		
	}
}
