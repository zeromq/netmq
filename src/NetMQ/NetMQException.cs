using System;
using System.Net.Sockets;
using NetMQ.zmq;

namespace NetMQ
{
	public class NetMQException : Exception
	{			
		protected NetMQException(ErrorCode errorCode)
		{
			ErrorCode = errorCode;
		}

		protected NetMQException(string message, ErrorCode errorCode)
			: base(message)
		{
			ErrorCode = errorCode;
		}

		protected NetMQException(ErrorCode errorCode, Exception ex)
			: base("", ex)
		{
			this.ErrorCode = errorCode;			
		}

		public ErrorCode ErrorCode { get; private set; }

		public static NetMQException Create(SocketException ex)
		{
			ErrorCode errorCode = ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode);

			return Create(errorCode, ex);
		}

		public static NetMQException Create(ErrorCode errorCode, Exception ex)
		{
			switch (errorCode)
			{
				case ErrorCode.EAGAIN:
					return new AgainException(ex);
				case ErrorCode.ETERM:
					return new TerminatingException(ex);
				case ErrorCode.EINVAL:
					return new InvalidException(ex);
				default:
					return new NetMQException(errorCode, ex);
			}
		}

		public static NetMQException Create(ErrorCode errorCode)
		{
			return Create("", errorCode);
		}

		public static NetMQException Create(string message, ErrorCode errorCode)
		{
			switch (errorCode)
			{
				case ErrorCode.EAGAIN:
					return new AgainException(message);
				case ErrorCode.ETERM:
					return new TerminatingException(message);
				case ErrorCode.EINVAL:
					return new InvalidException(message);
				default:
					return new NetMQException(message, errorCode);
			}
		}
	}

	public class AgainException : NetMQException
	{				
		internal AgainException(string message)
			: base(message, ErrorCode.EAGAIN)
		{
		}

		internal AgainException(Exception ex)
			: base(ErrorCode.EAGAIN, ex)
		{
		}		

		public static AgainException Create()
		{
			return new AgainException("");
		}
	}

	public class TerminatingException : NetMQException
	{		
		internal TerminatingException(string message)
			: base(message, ErrorCode.ETERM)
		{
		}

		internal TerminatingException(Exception ex)
			: base(ErrorCode.ETERM, ex)
		{
		}

		public static TerminatingException Create()
		{
			return new TerminatingException("");
		}
	}

	public class InvalidException : NetMQException
	{
		internal InvalidException(string message)
			: base(message, ErrorCode.EINVAL)
		{
		}

		internal InvalidException(Exception ex)
			: base(ErrorCode.EINVAL, ex)
		{
		}

		public static InvalidException Create()
		{
			return new InvalidException("");
		}

		public static InvalidException Create(string message)
		{
			return new InvalidException(message);
		}


		public static InvalidException Create(Exception ex)
		{
			return new InvalidException("");
		}
	}

	
}
