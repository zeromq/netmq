using System;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// ErrorPollingException is an Exception that had been used within the message-queueing system to signal polling-related errors.
    /// Use one of the NetMQException-derived exception classes instead.
    /// </summary>
    [Obsolete]
    public class ErrorPollingException : Exception
    {
        public ErrorPollingException([CanBeNull] string message, [NotNull] NetMQSocket socket)
            : base(message)
        {
            Socket = socket;
        }

        [NotNull]
        public NetMQSocket Socket { get; private set; }
    }
}
