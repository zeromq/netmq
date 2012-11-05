namespace ZeroMQ.Interop
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    internal static class ErrorProxy
    {
        private static readonly Dictionary<int, ErrorDetails> KnownErrors = new Dictionary<int, ErrorDetails>();

        public static bool ShouldTryAgain
        {
            get { return GetErrorCode() == ErrorCode.EAGAIN; }
        }

        public static bool ContextWasTerminated
        {
            get { return GetErrorCode() == ErrorCode.ETERM; }
        }

        public static bool ThreadWasInterrupted
        {
            get { return GetErrorCode() == ErrorCode.EINTR; }
        }

        public static int GetErrorCode()
        {
            return LibZmq.zmq_errno();
        }

        public static ErrorDetails GetLastError()
        {
            int errorCode = GetErrorCode();

            if (KnownErrors.ContainsKey(errorCode))
            {
                return KnownErrors[errorCode];
            }

            string message = Marshal.PtrToStringAnsi(LibZmq.zmq_strerror(errorCode));

            var errorDetails = new ErrorDetails(errorCode, message);
            KnownErrors[errorCode] = errorDetails;

            return errorDetails;
        }
    }
}
