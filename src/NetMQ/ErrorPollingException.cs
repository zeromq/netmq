using System;
using JetBrains.Annotations;

namespace NetMQ
{
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
