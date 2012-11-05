namespace ZeroMQ.Interop
{
    using System;

    internal sealed class ZmqMsgT : IDisposable
    {
        private DisposableIntPtr _ptr;
        private bool _initialized;

        public ZmqMsgT()
        {
            _ptr = new DisposableIntPtr(LibZmq.ZmqMsgTSize);
        }

        ~ZmqMsgT()
        {
            Dispose(false);
        }

        public IntPtr Ptr
        {
            get { return _ptr.Ptr; }
        }

        public static implicit operator IntPtr(ZmqMsgT msg)
        {
            return msg._ptr;
        }

        public int Init()
        {
            int rc = LibZmq.zmq_msg_init(_ptr);

            _initialized = true;

            return rc;
        }

        public int Init(int size)
        {
            int rc = LibZmq.zmq_msg_init_size(_ptr, size);

            _initialized = true;

            return rc;
        }

        public int Close()
        {
            int rc = LibZmq.zmq_msg_close(_ptr);

            _initialized = false;

            return rc;
        }

        public int Size()
        {
            return LibZmq.zmq_msg_size(_ptr);
        }

        public IntPtr Data()
        {
            return LibZmq.zmq_msg_data(_ptr);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void Dispose(bool disposing)
        {
            if (disposing && _initialized)
            {
                Close();
            }

            _ptr.Dispose();
        }
    }
}
