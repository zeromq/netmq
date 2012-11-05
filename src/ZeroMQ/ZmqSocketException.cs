namespace ZeroMQ
{
    using System;
    using System.Runtime.Serialization;    

    /// <summary>
    /// The exception that is thrown when a ZeroMQ socket error occurs.
    /// </summary>
    [Serializable]
    public class ZmqSocketException : ZmqException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqSocketException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        public ZmqSocketException(int errorCode)
            : base(errorCode)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqSocketException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        /// <param name="message">The message that describes the error</param>
        public ZmqSocketException(int errorCode, string message)
            : base(errorCode, message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqSocketException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        /// <param name="message">The message that describes the error</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public ZmqSocketException(int errorCode, string message, Exception inner)
            : base(errorCode, message, inner)
        {
        }

        internal ZmqSocketException(ErrorDetails errorDetails)
            : base(errorDetails)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqSocketException"/> class.
        /// </summary>
        /// <param name="info"><see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context"><see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected ZmqSocketException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
