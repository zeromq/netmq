
namespace NetMQ
{
	/// <summary>
	/// This Proxy class is used to forward messages from one socket to another.
	/// It also has a provision for you to specify a control-socket which both sockets would send messages to.
	/// </summary>
	public class Proxy
	{
		NetMQSocket m_frontend;
		NetMQSocket m_backend;
		NetMQSocket m_control;

        /// <summary>
        /// Create a new instance of a Proxy (NetMQ.Proxy)
        /// with the given sockets to serve as a front-end, a back-end, and a control socket.
        /// </summary>
        /// <param name="frontend">the socket that messages will be forwarded from</param>
        /// <param name="backend">the socket that messages will be forwarded to</param>
        /// <param name="control">this socket will have messages also sent to it - you can set this to null if not needed</param>
		public Proxy(NetMQSocket frontend, NetMQSocket backend, NetMQSocket control)
		{
			m_frontend = frontend;
			m_backend = backend;
			m_control = control;
		}

		/// <summary>
		/// Start the proxy working; this will block until one of the sockets is closed.
		/// </summary>
		public void Start()
		{
			zmq.ZMQ.Proxy(m_frontend.SocketHandle, m_backend.SocketHandle, m_control != null ? m_control.SocketHandle : null);
		}
	}
}
