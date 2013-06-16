using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace NetMQ.zmq
{
	public static class ErrorHelper
	{
		public static ErrorCode SocketErrorToErrorCode(SocketError error)
		{
			switch (error)
			{								
				case SocketError.AccessDenied:
					return ErrorCode.EACCESS;
				case SocketError.Fault:
					return ErrorCode.EFAULT;
				case SocketError.InvalidArgument:
					return ErrorCode.EINVAL;
				case SocketError.TooManyOpenSockets:
					return ErrorCode.EMFILE;
				case SocketError.InProgress:
					return ErrorCode.EAGAIN;
				case SocketError.MessageSize:
					return ErrorCode.EMSGSIZE;
				case SocketError.ProtocolNotSupported:
					return ErrorCode.EPROTONOSUPPORT;					
				case SocketError.AddressFamilyNotSupported:
					return ErrorCode.EAFNOSUPPORT;
				case SocketError.AddressAlreadyInUse:
					return ErrorCode.EADDRINUSE;
				case SocketError.AddressNotAvailable:
					return ErrorCode.EADDRNOTAVAIL;
				case SocketError.NetworkDown:
					return ErrorCode.ENETDOWN;
				case SocketError.NetworkUnreachable:
					return ErrorCode.ENETUNREACH;
				case SocketError.NetworkReset:
					return ErrorCode.ENETRESET;
				case SocketError.ConnectionAborted:
					return ErrorCode.ECONNABORTED;
				case SocketError.ConnectionReset:
					return ErrorCode.ECONNRESET;
				case SocketError.NoBufferSpaceAvailable:
					return ErrorCode.ENOBUFS;
				case SocketError.NotConnected:
					return ErrorCode.ENOTCONN;
				case SocketError.TimedOut:
					return ErrorCode.ETIMEDOUT;
				case SocketError.ConnectionRefused:
					return ErrorCode.ECONNREFUSED;
				case SocketError.HostUnreachable:
					return ErrorCode.EHOSTUNREACH;
				default:
					Debug.Assert(false);
					return 0;
			}
		}
	}
}
