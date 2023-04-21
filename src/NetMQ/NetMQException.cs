using System;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Security.Permissions;
using NetMQ.Core;

namespace NetMQ
{
    /// <summary>
    /// Base class for custom exceptions within the NetMQ library.
    /// </summary>
    [Serializable]
    public class NetMQException : Exception
    {
        /// <summary>
        /// Exception error code
        /// </summary>
        public ErrorCode ErrorCode { get; }

        #region Exception contract & serialisation

        // For discussion of this contract, see https://msdn.microsoft.com/en-us/library/ms182151.aspx

        /// <summary>
        /// Create NetMQ Exception
        /// </summary>
        public NetMQException()
        {}

        /// <summary>
        /// Create a new NetMQ exception
        /// </summary>
        /// <param name="message"></param>
        public NetMQException(string message)
            : base(message)
        {}

        /// <summary>
        /// Create a new NetMQ exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public NetMQException(string message, Exception innerException)
            : base(message, innerException)
        {}

        /// <summary>Constructor for serialisation.</summary>
        protected NetMQException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = (ErrorCode)info.GetInt32("ErrorCode");
        }

        /// <inheritdoc />
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ErrorCode", ErrorCode);
            base.GetObjectData(info, context);
        }

        #endregion

        /// <summary>
        /// Create a new NetMQException containing the given Exception, Message and ErrorCode.
        /// </summary>
        /// <param name="innerException">an Exception that this exception will expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="errorCode">an ErrorCode that this exception will expose via its ErrorCode property</param>
        protected NetMQException(Exception? innerException, string? message, ErrorCode errorCode)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing only the given SocketException.
        /// </summary>
        /// <param name="innerException">a SocketException that this exception will expose via its InnerException property</param>
        /// <returns>a new NetMQException</returns>
        public static NetMQException Create(SocketException innerException)
        {
            return Create(innerException.SocketErrorCode, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing the given SocketError and Exception.
        /// </summary>
        /// <param name="error">a SocketError that this exception will carry and expose via its ErrorCode property</param>
        /// <param name="innerException">an Exception that this exception will expose via its InnerException property</param>
        /// <returns>a new NetMQException</returns>
        public static NetMQException Create(SocketError error, Exception? innerException = null)
        {
            var errorCode = error.ToErrorCode();

#if DEBUG
            if (errorCode == 0)
            {
                var s = $"(And within NetMQException.Create: Unanticipated error-code: {error})";
                return Create(errorCode: errorCode, message: s, innerException: innerException);
            }
#endif

            return Create(errorCode, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing the given ErrorCode and Exception.
        /// </summary>
        /// <param name="errorCode">an ErrorCode for this exception to contain and expose via its ErrorCode property</param>
        /// <param name="innerException">an Exception for this exception to contain and expose via its InnerException property</param>
        /// <returns>a new NetMQException</returns>
        public static NetMQException Create(ErrorCode errorCode, Exception? innerException)
        {
            return Create(errorCode, null, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing only the given ErrorCode.
        /// </summary>
        /// <param name="errorCode">an ErrorCode that this exception will carry and expose via its ErrorCode property</param>
        /// <returns>a new NetMQException</returns>
        public static NetMQException Create(ErrorCode errorCode)
        {
            return Create(null, errorCode);
        }

        /// <summary>
        /// Create and return a new NetMQException with the given Message and ErrorCode.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="errorCode">an ErrorCode that this exception will carry and expose via its ErrorCode property</param>
        /// <returns>a new NetMQException</returns>
        public static NetMQException Create(string? message, ErrorCode errorCode)
        {
            return Create(errorCode, message, null);
        }

        /// <summary>
        /// Create and return a new NetMQException with the given ErrorCode, Message, and Exception.
        /// </summary>
        /// <param name="errorCode">an ErrorCode that this exception will contain and expose via its ErrorCode property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="innerException">an Exception that this exception will expose via its InnerException property</param>
        /// <returns>a new NetMQException, or subclass of NetMQException that corresponds to the given ErrorCode</returns>
        private static NetMQException Create(ErrorCode errorCode, string? message, Exception? innerException)
        {
            switch (errorCode)
            {
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
    [Serializable]
    public class AddressAlreadyInUseException : NetMQException
    {
        /// <summary>
        /// Create a new AddressAlreadyInUseException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public AddressAlreadyInUseException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.AddressAlreadyInUse)
        {
        }

        /// <summary>
        /// Create a new AddressAlreadyInUseException with a given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public AddressAlreadyInUseException(string? message)
            : this(null, message)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected AddressAlreadyInUseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public EndpointNotFoundException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.EndpointNotFound)
        {
        }

        /// <summary>
        /// Create a new EndpointNotFoundException with a given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public EndpointNotFoundException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new EndpointNotFoundException with no message nor inner-exception.
        /// </summary>
        public EndpointNotFoundException()
            : this(null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected EndpointNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal TerminatingException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.ContextTerminated)
        {
        }

        /// <summary>
        /// Create new TerminatingException
        /// </summary>
        /// <param name="message"></param>
        public TerminatingException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new TerminatingException with no message nor inner-exception.
        /// </summary>
        internal TerminatingException()
            : this(null, null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected TerminatingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public InvalidException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.Invalid)
        {
        }

        /// <summary>
        /// Create a new InvalidException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public InvalidException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new InvalidException with no message nor inner-exception.
        /// </summary>
        public InvalidException()
            : this(null, null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected InvalidException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// FaultException is a NetMQException that is used within the message-queueing system to signal general fault conditions.
    /// </summary>
    [Serializable]
    public class FaultException : NetMQException
    {
        /// <summary>
        /// Create a new FaultException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FaultException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.Fault)
        {
        }

        /// <summary>
        /// Create a new FaultException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FaultException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new FaultException with no message nor inner-exception.
        /// </summary>
        public FaultException()
            : this(null, null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected FaultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal ProtocolNotSupportedException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.ProtocolNotSupported)
        {
        }

        /// <summary>
        /// Create a new ProtocolNotSupportedException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal ProtocolNotSupportedException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new ProtocolNotSupportedException with no message nor inner-exception.
        /// </summary>
        public ProtocolNotSupportedException()
            : this(null, null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected ProtocolNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// HostUnreachableException is an Exception that is used within the message-queueing system
    /// to signal failures to communicate with a host.
    /// </summary>
    [Serializable]
    public class HostUnreachableException : NetMQException
    {
        /// <summary>
        /// Create a new HostUnreachableException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal HostUnreachableException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.HostUnreachable)
        {
        }

        /// <summary>
        /// Create a new HostUnreachableException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal HostUnreachableException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new HostUnreachableException with no message nor inner-exception.
        /// </summary>
        public HostUnreachableException()
            : this(null, null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected HostUnreachableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FiniteStateMachineException(Exception? innerException, string? message)
            : base(innerException, message, ErrorCode.FiniteStateMachine)
        {
        }

        /// <summary>
        /// Create a new FiniteStateMachineException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal FiniteStateMachineException(string? message)
            : this(null, message)
        {
        }

        /// <summary>
        /// Create a new FiniteStateMachineException with no message nor inner-exception.
        /// </summary>
        public FiniteStateMachineException()
            : this(null, null)
        {
        }

        /// <summary>Constructor for serialisation.</summary>
        protected FiniteStateMachineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
