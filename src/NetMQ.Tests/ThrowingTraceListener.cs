using System;
using System.Diagnostics;

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
        }
    }
}
