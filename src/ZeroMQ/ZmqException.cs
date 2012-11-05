namespace ZeroMQ
{
    using System;
    using System.Runtime.Serialization;
    

    /// <summary>
    /// An exception thrown by the result of a ZeroMQ library call.
    /// </summary>
    [Serializable]
    public class ZmqException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        public ZmqException(int errorCode)
        {
            this.ErrorCode = errorCode;
            this.ErrorName = GetErrorName(errorCode);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        /// <param name="message">The message that describes the error</param>
        public ZmqException(int errorCode, string message)
            : base(message)
        {
            this.ErrorCode = errorCode;
            this.ErrorName = GetErrorName(errorCode);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqException"/> class.
        /// </summary>
        /// <param name="errorCode">The error code returned by the ZeroMQ library call.</param>
        /// <param name="message">The message that describes the error</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public ZmqException(int errorCode, string message, Exception inner)
            : base(message, inner)
        {
            this.ErrorCode = errorCode;
            this.ErrorName = GetErrorName(errorCode);
        }

        internal ZmqException(ErrorDetails errorDetails)
            : this(errorDetails.ErrorCode, errorDetails.Message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqException"/> class.
        /// </summary>
        /// <param name="info"><see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context"><see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected ZmqException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets the error code returned by the ZeroMQ library call.
        /// </summary>
        public int ErrorCode { get; private set; }

        /// <summary>
        /// Gets the string representation of the error code, as found in the ZeroMQ docs.
        /// </summary>
        public string ErrorName { get; private set; }

        private static string GetErrorName(int errorCode)
        {
            return ZeroMQ.ErrorCode.ErrorNames.ContainsKey(errorCode)
                       ? ZeroMQ.ErrorCode.ErrorNames[errorCode]
                       : "Error " + errorCode;
        }
    }
}
