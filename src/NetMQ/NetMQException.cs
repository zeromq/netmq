using System;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// Class NetMQException is the parent-class for Exceptions that occur within the NetMQ library.
    /// </summary>
    [Serializable]
    public class NetMQException : Exception
    {
        /// <summary>
        /// Create a new NetMQException containing the given Exception, Message and ErrorCode.
        /// </summary>
        /// <param name="innerException">an Exception that this exception will expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="errorCode">an ErrorCode that this exception will expose via it's ErrorCode property</param>
        protected NetMQException([CanBeNull] Exception innerException, [CanBeNull] string message, ErrorCode errorCode)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public ErrorCode ErrorCode { get; private set; }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing only the given SocketException.
        /// </summary>
        /// <param name="innerException">a SocketException that this exception will expose via it's InnerException property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create([NotNull] SocketException innerException)
        {
            return Create(innerException.SocketErrorCode, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing the given SocketError and Exception.
        /// </summary>
        /// <param name="error">a SocketError that this exception will carry and expose via it's ErrorCode property</param>
        /// <param name="innerException">an Exception that this exception will expose via it's InnerException property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create(SocketError error, [CanBeNull] Exception innerException = null)
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
                    errorCode = 0; // to indicate no valid SocketError.
#if DEBUG
                    string s = String.Format("(And within NetMQException.Create: Unanticipated error-code: {0})", error.ToString());
                    return Create(errorCode: errorCode, message: s, innerException: innerException);
#else
                    break;
#endif
            }

            return Create(errorCode, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing the given ErrorCode and Exception.
        /// </summary>
        /// <param name="errorCode">an ErrorCode for this exception to contain and expose via it's ErrorCode property</param>
        /// <param name="innerException">an Exception for this exception to contain and expose via it's InnerException property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create(ErrorCode errorCode, [CanBeNull] Exception innerException)
        {
            return Create(errorCode, "", innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing only the given ErrorCode.
        /// </summary>
        /// <param name="errorCode">an ErrorCode that this exception will carry and expose via it's ErrorCode property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create(ErrorCode errorCode)
        {
            return Create("", errorCode);
        }

        /// <summary>
        /// Create and return a new NetMQException with the given Message and ErrorCode.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="errorCode">an ErrorCode that this exception will carry and expose via it's ErrorCode property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create([CanBeNull] string message, ErrorCode errorCode)
        {
            return Create(errorCode, message, null);
        }

        /// <summary>
        /// Create and return a new NetMQException with the given ErrorCode, Message, and Exception.
        /// </summary>
        /// <param name="errorCode">an ErrorCode that this exception will contain and expose via it's ErrorCode property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="innerException">an Exception that this exception will expose via it's InnerException property</param>
        /// <returns>a new NetMQException, or subclass of NetMQException that corresponds to the given ErrorCode</returns>
        [NotNull]
        private static NetMQException Create(ErrorCode errorCode, [CanBeNull] string message, [CanBeNull] Exception innerException)
        {
            switch (errorCode)
            {
                case ErrorCode.TryAgain:
                    return new AgainException(innerException, message);
                case ErrorCode.ContextTerminated:
                    return new TerminatingException(innerException, message);
                case ErrorCode.Invalid:
                    return new InvalidException(innerException, message);
                case ErrorCode.EndpointNotFound:
                    return new EndpointNotFoundException(innerException, message);
                case ErrorCode.AddressAlreadyInUse:
                    return new AddressAlreadyInUseException(innerException, message);
                case ErrorCode.ProtocolNotSupported:
                    return new ProtocolNotSupportedException(innerException, message);
                case ErrorCode.HostUnreachable:
                    return new HostUnreachableException(innerException, message);
                case ErrorCode.FiniteStateMachine:
                    return new FiniteStateMachineException(innerException, message);
                case ErrorCode.Fault:
                    return new FaultException(innerException, message);
                default:
                    return new NetMQException(innerException, message, errorCode);
            }
        }
    }

    /// <summary>
    /// AddressAlreadyInUseException is a NetMQException that is used within SocketBase.Bind to signal an address-conflict.
    /// </summary>
    public class AddressAlreadyInUseException : NetMQException
    {
        /// <summary>
        /// Create a new AddressAlreadyInUseException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public AddressAlreadyInUseException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.AddressAlreadyInUse)
        {
        }

        /// <summary>
        /// Create a new AddressAlreadyInUseException with a given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public AddressAlreadyInUseException([CanBeNull] string message)
            : this(null, message)
        {
        }
    }

    /// <summary>
    /// EndpointNotFoundException is a NetMQException that is used within Ctx.FindEndpoint to signal a failure to find a specified address.
    /// </summary>
    [Serializable]
    public class EndpointNotFoundException : NetMQException
    {
        /// <summary>
        /// Create a new EndpointNotFoundException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public EndpointNotFoundException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.EndpointNotFound)
        {
        }

        /// <summary>
        /// Create a new EndpointNotFoundException with a given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public EndpointNotFoundException([CanBeNull] string message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new EndpointNotFoundException with no message nor inner-exception.
        /// </summary>
        public EndpointNotFoundException()
            : this("")
        {
        }
    }

    /// <summary>
    /// AgainException is a NetMQException that is used within SocketBase.Send and SocketBase.Recv to signal failures
    /// (as when the Send/Receive fails and DontWait is set or no timeout is specified)
    /// and is raised within Sub.XSetSocketOption if sending the queued-message fails.
    /// </summary>
    [Serializable]
    public class AgainException : NetMQException
    {
        /// <summary>
        /// Create a new AgainException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal AgainException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.TryAgain)
        {
        }

        /// <summary>
        /// Create a new AgainException with no message nor inner-exception.
        /// </summary>
        public AgainException()
            : this(null, "")
        {
        }
    }

    /// <summary>
    /// TerminatingException is a NetMQException that is used within SocketBase and Ctx to signal
    /// that you're making the mistake of trying to do further work after terminating the message-queueing system.
    /// </summary>
    [Serializable]
    public class TerminatingException : NetMQException
    {
        /// <summary>
        /// Create a new TerminatingException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal TerminatingException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.ContextTerminated)
        {
        }

        /// <summary>
        /// Create a new TerminatingException with no message nor inner-exception.
        /// </summary>
        internal TerminatingException()
            : this(null, "")
        {
        }
    }

    /// <summary>
    /// InvalidException is a NetMQException that is used within the message-queueing system to signal invalid value errors.
    /// </summary>
    [Serializable]
    public class InvalidException : NetMQException
    {
        /// <summary>
        /// Create a new InvalidException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal InvalidException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.Invalid)
        {
        }

        /// <summary>
        /// Create a new InvalidException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal InvalidException([CanBeNull] string message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new InvalidException with no message nor inner-exception.
        /// </summary>
        public InvalidException()
            : this(null, "")
        {
        }
    }

    /// <summary>
    /// FaultException is a NetMQException that is used within within the message-queueing system to signal general fault conditions.
    /// </summary>
    [Serializable]
    public class FaultException : NetMQException
    {
        /// <summary>
        /// Create a new FaultException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FaultException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.Fault)
        {
        }

        /// <summary>
        /// Create a new FaultException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FaultException([CanBeNull] string message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new FaultException with no message nor inner-exception.
        /// </summary>
        public FaultException()
            : this(null, "")
        {
        }
    }

    /// <summary>
    /// ProtocolNotSupportedException is a NetMQException that is used within the message-queueing system to signal
    /// mistakes in properly utilizing the communications protocols.
    /// </summary>
    [Serializable]
    public class ProtocolNotSupportedException : NetMQException
    {
        /// <summary>
        /// Create a new ProtocolNotSupportedException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal ProtocolNotSupportedException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.ProtocolNotSupported)
        {
        }

        /// <summary>
        /// Create a new ProtocolNotSupportedException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal ProtocolNotSupportedException([CanBeNull] string message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new ProtocolNotSupportedException with no message nor inner-exception.
        /// </summary>
        public ProtocolNotSupportedException()
            : this(null, "")
        {
        }
    }

    /// <summary>
    /// HostUnreachableException is an Exception that is used within within the message-queueing system
    /// to signal failures to communicate with a host.
    /// </summary>
    [Serializable]
    public class HostUnreachableException : NetMQException
    {
        /// <summary>
        /// Create a new HostUnreachableException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal HostUnreachableException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.HostUnreachable)
        {
        }

        /// <summary>
        /// Create a new HostUnreachableException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal HostUnreachableException([CanBeNull] string message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new HostUnreachableException with no message nor inner-exception.
        /// </summary>
        public HostUnreachableException()
            : this(null, "")
        {
        }
    }

    /// <summary>
    /// FiniteStateMachineException is an Exception that is used within the message-queueing system
    /// to signal errors in the send/receive order with request/response sockets.
    /// </summary>
    [Serializable]
    public class FiniteStateMachineException : NetMQException
    {
        /// <summary>
        /// Create a new FiniteStateMachineException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FiniteStateMachineException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.FiniteStateMachine)
        {
        }

        /// <summary>
        /// Create a new FiniteStateMachineException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FiniteStateMachineException([CanBeNull] string message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new FiniteStateMachineException with no message nor inner-exception.
        /// </summary>
        public FiniteStateMachineException()
            : this(null, "")
        {
        }
    }
}
