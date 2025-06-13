using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace NetMQ.Tests
{
    public sealed class ThrowingTraceListener : TraceListener
    {
        public override void Fail(string? message, string? detailMessage)
        {
            throw new DebugAssertFailureException((message + Environment.NewLine + detailMessage).Trim());
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }

        [Serializable]
        public class DebugAssertFailureException : Exception
        {
            public DebugAssertFailureException()
            {
            }

            public DebugAssertFailureException(string message) : base(message)
            {
            }

            public DebugAssertFailureException(string message, Exception inner) : base(message, inner)
            {
            }

            protected DebugAssertFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
