using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public interface IOutgoingSocket
	{
		void Send(byte[] data, int length, SendReceiveOptions options);
		void Send(byte[] data);

		void Send(byte[] data, int length);
		void Send(byte[] data, int length, bool dontWait, bool sendMore);

		void Send(string message, bool dontWait, bool sendMore);

		void Send(string message);

		IOutgoingSocket SendMore(string message);

		IOutgoingSocket SendMore(string message, bool dontWait);

		IOutgoingSocket SendMore(byte[] data);

		IOutgoingSocket SendMore(byte[] data, bool dontWait);

		IOutgoingSocket SendMore(byte[] data, int length);

		IOutgoingSocket SendMore(byte[] data, int length, bool dontWait);
	}
}
