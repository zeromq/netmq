using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
	/// <summary>
	/// Forward messages between two sockets, you can also specify control socket which both sockets will send messages to
	/// </summary>
	public class Proxy
	{
		BaseSocket m_frontend;
		BaseSocket m_backend;
		BaseSocket m_control;

		public Proxy(BaseSocket frontend, BaseSocket backend, BaseSocket control)
		{
			m_frontend = frontend;
			m_backend = backend;
			m_control = control;
		}

		/// <summary>
		/// Start the proxy work, this will block until one of the sockets is closed
		/// </summary>
		public void Start()
		{
			zmq.ZMQ.Proxy(m_frontend.SocketHandle, m_backend.SocketHandle, m_control != null ? m_control.SocketHandle : null);
		}
	}
}
