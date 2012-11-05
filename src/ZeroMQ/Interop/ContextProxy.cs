namespace ZeroMQ.Interop
{
    using System;

    internal class ContextProxy : IDisposable
    {
        private bool _disposed;

        ~ContextProxy()
        {
            Dispose(false);
        }

        public IntPtr ContextHandle { get; private set; }

        public int Initialize()
        {
            ContextHandle = LibZmq.zmq_ctx_new();

            return ContextHandle == IntPtr.Zero ? -1 : 0;
        }

        public IntPtr CreateSocket(int socketType)
        {
            return LibZmq.zmq_socket(ContextHandle, socketType);
        }

        public void Terminate()
        {
            if (ContextHandle == IntPtr.Zero)
            {
                return;
            }

            while (LibZmq.zmq_ctx_destroy(ContextHandle) != 0)
            {
                int errorCode = ErrorProxy.GetErrorCode();

                // If zmq_ctx_destroy fails, valid return codes are EFAULT or EINTR. If EINTR is set, termination
                // was interrupted by a signal and may be safely retried.
                if (errorCode == ErrorCode.EFAULT)
                {
                    // This indicates an invalid context was passed in. There's nothing we can do about it here.
                    // It's arguably not a fatal error, so throwing an exception would be bad seeing as this may
                    // run inside a finalizer.
                    break;
                }
            }

            ContextHandle = IntPtr.Zero;
        }

        public int GetContextOption(int option)
        {
            return LibZmq.zmq_ctx_get(ContextHandle, option);
        }

        public int SetContextOption(int option, int value)
        {
            return LibZmq.zmq_ctx_set(ContextHandle, option, value);
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Terminate();
            }

            _disposed = true;
        }
    }
}
