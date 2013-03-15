using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
	public class ErrorPollingException : Exception
	{
		public ErrorPollingException(string message, NetMQSocket socket) : base(message)
		{
			Socket = socket;
		}

		public NetMQSocket Socket { get; private set; }
	}
}
