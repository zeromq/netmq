using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace NetMQ
{
    [Serializable]
    public class NetMQException : Exception
    {
        protected NetMQException(Exception ex, string message, ErrorCode errorCode)
            : base(message, ex)
        {
            ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; private set; }

        public static NetMQException Create(SocketError error)
        {
            return Create(error, null);
        }

        public static NetMQException Create(SocketException ex)
        {
            return Create(ex.SocketErrorCode, ex);
        }

        public static NetMQException Create(SocketError error, Exception ex)
        {
            ErrorCode errorCode;

            switch (error)
            {
                case SocketError.AccessDenied:
                    errorCode = ErrorCode.AccessDenied;
                    break;
                case SocketError.Fault:
                    errorCode = ErrorCode.Fault;
                    break;
                case SocketError.InvalidArgument:
                    errorCode = ErrorCode.Invalid;
                    break;
                case SocketError.TooManyOpenSockets:
                    errorCode = ErrorCode.TooManyOpenSockets;
                    break;
                case SocketError.InProgress:
                    errorCode = ErrorCode.TryAgain;
                    break;
                case SocketError.MessageSize:
                    errorCode = ErrorCode.MessageSize;
                    break;
                case SocketError.ProtocolNotSupported:
                    errorCode = ErrorCode.ProtocolNotSupported;
                    break;
                case SocketError.AddressFamilyNotSupported:
                    errorCode = ErrorCode.AddressFamilyNotSupported;
                    break;
                case SocketError.AddressNotAvailable:
                    errorCode = ErrorCode.AddressNotAvailable;
                    break;
                case SocketError.NetworkDown:
                    errorCode = ErrorCode.NetworkDown;
                    break;
                case SocketError.NetworkUnreachable:
                    errorCode = ErrorCode.NetworkUnreachable;
                    break;
                case SocketError.NetworkReset:
                    errorCode = ErrorCode.NetworkReset;
                    break;
                case SocketError.ConnectionAborted:
                    errorCode = ErrorCode.ConnectionAborted;
                    break;
                case SocketError.ConnectionReset:
                    errorCode = ErrorCode.ConnectionReset;
                    break;
                case SocketError.NoBufferSpaceAvailable:
                    errorCode = ErrorCode.NoBufferSpaceAvailable;
                    break;
                case SocketError.NotConnected:
                    errorCode = ErrorCode.NotConnected;
                    break;
                case SocketError.TimedOut:
                    errorCode = ErrorCode.TimedOut;
                    break;
                case SocketError.ConnectionRefused:
                    errorCode = ErrorCode.ConnectionRefused;
                    break;
                case SocketError.HostUnreachable:
                    errorCode = ErrorCode.HostUnreachable;
                    break;
                default:
                    Debug.Assert(false);
                    errorCode = 0;
                    break;
            }

            return Create(errorCode, ex);
        }

        public static NetMQException Create(ErrorCode errorCode, Exception ex)
        {
            return Create(errorCode, "", ex);
        }

        public static NetMQException Create(ErrorCode errorCode)
        {
            return Create("", errorCode);
        }

        public static NetMQException Create(string message, ErrorCode errorCode)
        {
            return Create(errorCode, message, null);
        }

        private static NetMQException Create(ErrorCode errorCode, string message, Exception ex)
        {
            switch (errorCode)
            {
                case ErrorCode.TryAgain:
                    return new AgainException(ex, message);
                case ErrorCode.ContextTerminated:
                    return new TerminatingException(ex, message);
                case ErrorCode.Invalid:
                    return new InvalidException(ex, message);
                case ErrorCode.EndpointNotFound:
                    return new EndpointNotFoundException(ex, message);
                case ErrorCode.AddressAlreadyInUse:
                    return new AddressAlreadyInUseException(ex, message);
                case ErrorCode.ProtocolNotSupported:
                    return new ProtocolNotSupportedException(ex, message);
                case ErrorCode.HostUnreachable:
                    return new HostUnreachableException(ex, message);
                case ErrorCode.FiniteStateMachine:
                    return new FiniteStateMachineException(ex, message);
                case ErrorCode.Fault:
                    return new FaultException(ex, message);
                default:
                    return new NetMQException(ex, message, errorCode);
            }
        }
    }

    public class AddressAlreadyInUseException : NetMQException
    {
        public AddressAlreadyInUseException(Exception ex, string message)
            : base(ex, message, ErrorCode.AddressAlreadyInUse)
        {
        }

        public AddressAlreadyInUseException(string message)
            : this(null, message)
        {
        }
    }

    [Serializable]
    public class EndpointNotFoundException : NetMQException
    {
        public EndpointNotFoundException(Exception ex, string message)
            : base(ex, message, ErrorCode.EndpointNotFound)
        {
        }

        public EndpointNotFoundException(string message)
            : this(null, message)
        {
        }

        public EndpointNotFoundException()
            : this("")
        {
        }
    }

    [Serializable]
    public class AgainException : NetMQException
    {
        internal AgainException(Exception ex, string message)
            : base(ex, message, ErrorCode.TryAgain)
        {
        }

        public AgainException()
            : this(null, "")
        {
        }
    }

    [Serializable]
    public class TerminatingException : NetMQException
    {
        internal TerminatingException(Exception ex, string message)
            : base(ex, message, ErrorCode.ContextTerminated)
        {
        }

        internal TerminatingException()
            : this(null, "")
        {
        }
    }

    [Serializable]
    public class InvalidException : NetMQException
    {
        internal InvalidException(Exception ex, string message)
            : base(ex, message, ErrorCode.Invalid)
        {
        }

        internal InvalidException(string message)
            : this(null, message)
        {
        }

        public InvalidException()
            : this(null, "")
        {
        }
    }

    [Serializable]
    public class FaultException : NetMQException
    {
        internal FaultException(Exception ex, string message)
            : base(ex, message, ErrorCode.Fault)
        {
        }

        internal FaultException(string message)
            : this(null, message)
        {
        }

        public FaultException()
            : this(null, "")
        {
        }
    }

    [Serializable]
    public class ProtocolNotSupportedException : NetMQException
    {
        internal ProtocolNotSupportedException(Exception ex, string message)
            : base(ex, message, ErrorCode.ProtocolNotSupported)
        {
        }

        internal ProtocolNotSupportedException(string message)
            : this(null, message)
        {
        }

        public ProtocolNotSupportedException()
            : this(null, "")
        {
        }
    }

    [Serializable]
    public class HostUnreachableException : NetMQException
    {
        internal HostUnreachableException(Exception ex, string message)
            : base(ex, message, ErrorCode.HostUnreachable)
        {
        }

        internal HostUnreachableException(string message)
            : this(null, message)
        {
        }

        public HostUnreachableException()
            : this(null, "")
        {
        }
    }

    [Serializable]
    public class FiniteStateMachineException : NetMQException
    {
        internal FiniteStateMachineException(Exception ex, string message)
            : base(ex, message, ErrorCode.FiniteStateMachine)
        {
        }

        internal FiniteStateMachineException(string message)
            : this(null, message)
        {
        }

        public FiniteStateMachineException()
            : this(null, "")
        {
        }
    }
}
