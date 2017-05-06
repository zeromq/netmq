using System;
using System.Net.Sockets;
using System.Runtime.Serialization;

#if !NETSTANDARD1_3 && !UAP
using System.Security.Permissions;
#endif

using JetBrains.Annotations;
using NetMQ.Core;

namespace NetMQ
{
    /// <summary>
    /// Base class for custom exceptions within the NetMQ library.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class NetMQException : Exception
    {
        public ErrorCode ErrorCode { get; }

        #region Exception contract & serialisation

        // For discussion of this contract, see https://msdn.microsoft.com/en-us/library/ms182151.aspx

        public NetMQException()
        {}

        public NetMQException(string message)
            : base(message)
        {}

        public NetMQException(string message, Exception innerException)
            : base(message, innerException)
        {}

#if !NETSTANDARD1_3 && !UAP

        /// <summary>Constructor for serialisation.</summary>
        protected NetMQException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = (ErrorCode)info.GetInt32("ErrorCode");
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ErrorCode", ErrorCode);
            base.GetObjectData(info, context);
        }

#endif

        #endregion

        /// <summary>
        /// Create a new NetMQException containing the given Exception, Message and ErrorCode.
        /// </summary>
        /// <param name="innerException">an Exception that this exception will expose via it's InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        /// <param name="errorCode">an ErrorCode that this exception will expose via its ErrorCode property</param>
        protected NetMQException([CanBeNull] Exception innerException, [CanBeNull] string message, ErrorCode errorCode)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing only the given SocketException.
        /// </summary>
        /// <param name="innerException">a SocketException that this exception will expose via its InnerException property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create([NotNull] SocketException innerException)
        {
            return Create(innerException.SocketErrorCode, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing the given SocketError and Exception.
        /// </summary>
        /// <param name="error">a SocketError that this exception will carry and expose via its ErrorCode property</param>
        /// <param name="innerException">an Exception that this exception will expose via its InnerException property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
        public static NetMQException Create(SocketError error, [CanBeNull] Exception innerException = null)
        {
            var errorCode = error.ToErrorCode();

#if DEBUG
            if (errorCode == 0)
            {
                var s = $"(And within NetMQException.Create: Unanticipated error-code: {error.ToString()})";
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
        [NotNull]
        public static NetMQException Create(ErrorCode errorCode, [CanBeNull] Exception innerException)
        {
            return Create(errorCode, null, innerException);
        }

        /// <summary>
        /// Create and return a new NetMQException with no Message containing only the given ErrorCode.
        /// </summary>
        /// <param name="errorCode">an ErrorCode that this exception will carry and expose via its ErrorCode property</param>
        /// <returns>a new NetMQException</returns>
        [NotNull]
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
        [NotNull]
        public static NetMQException Create([CanBeNull] string message, ErrorCode errorCode)
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
        [NotNull]
        private static NetMQException Create(ErrorCode errorCode, [CanBeNull] string message, [CanBeNull] Exception innerException)
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
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class AddressAlreadyInUseException : NetMQException
    {
        /// <summary>
        /// Create a new AddressAlreadyInUseException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
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

#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected AddressAlreadyInUseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// EndpointNotFoundException is a NetMQException that is used within Ctx.FindEndpoint to signal a failure to find a specified address.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class EndpointNotFoundException : NetMQException
    {
        /// <summary>
        /// Create a new EndpointNotFoundException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
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
            : this(null)
        {
        }

#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected EndpointNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// TerminatingException is a NetMQException that is used within SocketBase and Ctx to signal
    /// that you're making the mistake of trying to do further work after terminating the message-queueing system.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class TerminatingException : NetMQException
    {
        /// <summary>
        /// Create a new TerminatingException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        internal TerminatingException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.ContextTerminated)
        {
        }

        public TerminatingException([CanBeNull] string message)
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
#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected TerminatingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// InvalidException is a NetMQException that is used within the message-queueing system to signal invalid value errors.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class InvalidException : NetMQException
    {
        /// <summary>
        /// Create a new InvalidException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public InvalidException([CanBeNull] Exception innerException, [CanBeNull] string message)
            : base(innerException, message, ErrorCode.Invalid)
        {
        }

        /// <summary>
        /// Create a new InvalidException with the given message.
        /// </summary>
        /// <param name="message">the textual description of what gave rise to this exception, to expose via the Message property</param>
        public InvalidException([CanBeNull] string message)
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
#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected InvalidException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// FaultException is a NetMQException that is used within the message-queueing system to signal general fault conditions.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class FaultException : NetMQException
    {
        /// <summary>
        /// Create a new FaultException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
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
            : this(null, null)
        {
        }
#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected FaultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// ProtocolNotSupportedException is a NetMQException that is used within the message-queueing system to signal
    /// mistakes in properly utilizing the communications protocols.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class ProtocolNotSupportedException : NetMQException
    {
        /// <summary>
        /// Create a new ProtocolNotSupportedException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
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
            : this(null, null)
        {
        }
#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected ProtocolNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// HostUnreachableException is an Exception that is used within the message-queueing system
    /// to signal failures to communicate with a host.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class HostUnreachableException : NetMQException
    {
        /// <summary>
        /// Create a new HostUnreachableException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
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
            : this(null, null)
        {
        }
#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected HostUnreachableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// FiniteStateMachineException is an Exception that is used within the message-queueing system
    /// to signal errors in the send/receive order with request/response sockets.
    /// </summary>
#if !NETSTANDARD1_3 && !UAP
    [Serializable]
#endif
    public class FiniteStateMachineException : NetMQException
    {
        /// <summary>
        /// Create a new FiniteStateMachineException with a given inner-exception and message.
        /// </summary>
        /// <param name="innerException">an Exception for this new exception to contain and expose via its InnerException property</param>
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
            : this(null, null)
        {
        }
#if !NETSTANDARD1_3 && !UAP
        /// <summary>Constructor for serialisation.</summary>
        protected FiniteStateMachineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
