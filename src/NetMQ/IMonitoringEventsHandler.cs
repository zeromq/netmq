using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public interface IMonitoringEventsHandler
	{
		SocketEvent Events { get; }

		void OnConnected(string address, IntPtr fd);

		void OnConnectDelayed(string address, ErrorCode errorCode);

		void OnConnectRetried(string address, int interval);

		void OnConnectFailed(string address, ErrorCode errorCode);
		void OnListening(string address, IntPtr fd);


		void OnBindFailed(string address, ErrorCode errorCode);

		void OnAccepted(string address, IntPtr fd);
		void OnAcceptFailed(string address, ErrorCode errorCode);

		void OnClosed(string address, IntPtr fd);
		void OnCloseFailed(string address, ErrorCode errorCode);
		void OnDisconnected(string address, IntPtr fd);
	}
}
