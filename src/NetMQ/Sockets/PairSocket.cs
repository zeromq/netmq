using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Pair socket, usually used to synchronize two threads, only one socket on each side
	/// </summary>
	class PairSocket : BaseSocket, IPairSocket
	{
		public PairSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		
	}
}
