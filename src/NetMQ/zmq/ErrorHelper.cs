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
					break;
				case SocketError.Fault:
					return ErrorCode.EFAULT;
					break;
				case SocketError.InvalidArgument:
					return ErrorCode.EINVAL;
					break;
				case SocketError.TooManyOpenSockets:
					return ErrorCode.EMFILE;
					break;
				case SocketError.InProgress:
					return ErrorCode.EAGAIN;
					break;
				case SocketError.MessageSize:
					return ErrorCode.EMSGSIZE;
					break;
				case SocketError.ProtocolNotSupported:
					return ErrorCode.EPROTONOSUPPORT;					
					break;
				case SocketError.AddressFamilyNotSupported:
					return ErrorCode.EAFNOSUPPORT;
					break;
				case SocketError.AddressAlreadyInUse:
					return ErrorCode.EADDRINUSE;
					break;
				case SocketError.AddressNotAvailable:
					return ErrorCode.EADDRNOTAVAIL;
					break;
				case SocketError.NetworkDown:
					return ErrorCode.ENETDOWN;
					break;
				case SocketError.NetworkUnreachable:
					return ErrorCode.ENETUNREACH;
					break;
				case SocketError.NetworkReset:
					return ErrorCode.ENETRESET;
					break;
				case SocketError.ConnectionAborted:
					return ErrorCode.ECONNABORTED;
					break;
				case SocketError.ConnectionReset:
					return ErrorCode.ECONNRESET;
					break;
				case SocketError.NoBufferSpaceAvailable:
					return ErrorCode.ENOBUFS;
					break;
				case SocketError.NotConnected:
					return ErrorCode.ENOTCONN;
					break;
				case SocketError.TimedOut:
					return ErrorCode.ETIMEDOUT;
					break;
				case SocketError.ConnectionRefused:
					return ErrorCode.ECONNREFUSED;
					break;
				case SocketError.HostUnreachable:
					return ErrorCode.EHOSTUNREACH;
					break;
				default:
					Debug.Assert(false);
					return 0;
			}
		}
	}
}
