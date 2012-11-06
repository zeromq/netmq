using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using zmq;

namespace NetMQ
{
	public class NetMQException : Exception
	{		
		private zmq.ErrorNumber m_errorNumber;

		public NetMQException()
		{
		}

		public NetMQException(string message) : base(message)
		{
		}

		public NetMQException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected NetMQException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}

		public NetMQException(string message, zmq.ErrorNumber errorNumber) : this(message)
		{			
			this.m_errorNumber = errorNumber;
		}
	}

	public class TryAgainException : NetMQException
	{
		public TryAgainException()
		{
		}

		public TryAgainException(string message) : base(message)
		{
		}

		public TryAgainException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected TryAgainException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}

		public TryAgainException(string message, ErrorNumber errorNumber) : base(message, errorNumber)
		{
		}
	}
}
