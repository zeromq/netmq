namespace ZeroMQ
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// The exception that is thrown when the current ZeroMQ version does not meet application requirements.
    /// </summary>
    [Serializable]
    public class ZmqVersionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqVersionException"/> class.
        /// </summary>
        public ZmqVersionException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqVersionException"/> class.
        /// </summary>
        /// <param name="major">The current major 0MQ version.</param>
        /// <param name="minor">The current minor 0MQ version.</param>
        /// <param name="requiredMajor">The required major 0MQ version.</param>
        /// <param name="requiredMinor">The required minor 0MQ version.</param>
        public ZmqVersionException(int major, int minor, int requiredMajor, int requiredMinor)
            : base(string.Format("Invalid 0MQ version. Current: {0}.{1}; required: {2}.{3}", major, minor, requiredMajor, requiredMinor))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqVersionException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public ZmqVersionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqVersionException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public ZmqVersionException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZmqVersionException"/> class.
        /// </summary>
        /// <param name="info"><see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context"><see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected ZmqVersionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
