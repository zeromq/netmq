namespace ZeroMQ.Interop
{
    internal class ErrorDetails
    {
        public ErrorDetails(int errorCode, string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public int ErrorCode { get; private set; }

        public string Message { get; private set; }
    }
}
