using System;

namespace NetMQ
{
    [Obsolete]
    public class ErrorPollingException : Exception
    {
        public ErrorPollingException(string message, NetMQSocket socket)
            : base(message)
        {
            Socket = socket;
        }

        public NetMQSocket Socket { get; private set; }
    }
}
