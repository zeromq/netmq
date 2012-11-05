namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.InteropServices;

    internal class SocketProxy : IDisposable
    {
        public const int MaxBufferSize = 8192;

        // From options.hpp: unsigned char identity [256];
        private const int MaxBinaryOptionSize = 255;

        private readonly ZmqMsgT _msg;
        private readonly IntPtr _buffer;

        private bool _disposed;

        public SocketProxy(IntPtr socketHandle)
        {
            if (socketHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Socket handle must be a valid pointer.", "socketHandle");
            }

            SocketHandle = socketHandle;

            _msg = new ZmqMsgT();
            _buffer = Marshal.AllocHGlobal(MaxBufferSize);
        }

        ~SocketProxy()
        {
            Dispose(false);
        }

        public IntPtr SocketHandle { get; private set; }

        public int Bind(string endpoint)
        {
            return LibZmq.zmq_bind(SocketHandle, endpoint);
        }

        public int Unbind(string endpoint)
        {
            return LibZmq.zmq_unbind(SocketHandle, endpoint);
        }

        public int Connect(string endpoint)
        {
            return LibZmq.zmq_connect(SocketHandle, endpoint);
        }

        public int Disconnect(string endpoint)
        {
            return LibZmq.zmq_disconnect(SocketHandle, endpoint);
        }

        public int Monitor(string endpoint, int events)
        {
            return LibZmq.zmq_socket_monitor(SocketHandle, endpoint, events);
        }

        public int Close()
        {
            // Allow Close to be called repeatedly without failure
            if (SocketHandle == IntPtr.Zero)
            {
                return 0;
            }

            int rc = LibZmq.zmq_close(SocketHandle);

            SocketHandle = IntPtr.Zero;

            return rc;
        }

        public int Receive(byte[] buffer, int flags)
        {
            int bytesReceived;

            // Use zmq_buffer_recv method if appropriate -> results in fewer P/Invoke calls
            if (buffer.Length <= MaxBufferSize && LibZmq.zmq_buffer_recv != null)
            {
                bytesReceived = Retry.IfInterrupted(LibZmq.zmq_buffer_recv.Invoke, SocketHandle, _buffer, MaxBufferSize, flags);
                int size = Math.Min(buffer.Length, bytesReceived);

                if (size > 0)
                {
                    Marshal.Copy(_buffer, buffer, 0, size);
                }

                return size;
            }

            if (_msg.Init(buffer.Length) == -1)
            {
                return -1;
            }

            bytesReceived = Retry.IfInterrupted(LibZmq.zmq_msg_recv.Invoke, _msg.Ptr, SocketHandle, flags);

            if (bytesReceived == 0 && LibZmq.MajorVersion < 3)
            {
                // 0MQ 2.x does not return number of bytes received
                bytesReceived = _msg.Size();
            }

            if (bytesReceived > 0)
            {
                Marshal.Copy(_msg.Data(), buffer, 0, bytesReceived);
            }

            if (_msg.Close() == -1)
            {
                return -1;
            }

            return bytesReceived;
        }

        public byte[] Receive(byte[] buffer, int flags, out int size)
        {
            size = -1;

            if (_msg.Init() == -1)
            {
                return buffer;
            }

            int bytesReceived = Retry.IfInterrupted(LibZmq.zmq_msg_recv.Invoke, _msg.Ptr, SocketHandle, flags);

            if (bytesReceived >= 0)
            {
                if (bytesReceived == 0 && LibZmq.MajorVersion < 3)
                {
                    // 0MQ 2.x does not return number of bytes received
                    bytesReceived = _msg.Size();
                }

                size = bytesReceived;

                if (buffer == null || size > buffer.Length)
                {
                    buffer = new byte[size];
                }

                Marshal.Copy(_msg.Data(), buffer, 0, size);
            }

            if (_msg.Close() == -1)
            {
                size = -1;
            }

            return buffer;
        }

        public int Send(byte[] buffer, int size, int flags)
        {
            // Use zmq_buffer_send method if appropriate -> results in fewer P/Invoke calls
            if (buffer.Length <= MaxBufferSize && LibZmq.zmq_buffer_send != null)
            {
                int sizeToSend = Math.Min(size, MaxBufferSize);
                Marshal.Copy(buffer, 0, _buffer, sizeToSend);

                return Retry.IfInterrupted(LibZmq.zmq_buffer_send.Invoke, SocketHandle, _buffer, sizeToSend, flags);
            }

            if (_msg.Init(size) == -1)
            {
                return -1;
            }

            if (size > 0)
            {
                Marshal.Copy(buffer, 0, _msg.Data(), size);
            }

            int bytesSent = Retry.IfInterrupted(LibZmq.zmq_msg_send.Invoke, _msg.Ptr, SocketHandle, flags);

            if (bytesSent == 0 && LibZmq.MajorVersion < 3)
            {
                // 0MQ 2.x does not report number of bytes sent, so this may be inaccurate/misleading
                bytesSent = size;
            }

            if (_msg.Close() == -1)
            {
                return -1;
            }

            return bytesSent;
        }

        public int Forward(IntPtr destinationHandle)
        {
            if (_msg.Init() == -1)
            {
                return -1;
            }

            int receiveMore;
            int bytesSent;

            do
            {
                if (LibZmq.zmq_msg_recv(_msg, SocketHandle, 0) == -1)
                {
                    return -1;
                }

                if (GetReceiveMore(out receiveMore) == -1)
                {
                    return -1;
                }

                if ((bytesSent = LibZmq.zmq_msg_send(_msg, destinationHandle, receiveMore == 1 ? (int)SocketFlags.SendMore : 0)) == -1)
                {
                    return -1;
                }
            }
            while (receiveMore == 1);

            if (_msg.Close() == -1)
            {
                return -1;
            }

            return bytesSent;
        }

        public int GetSocketOption(int option, out int value)
        {
            using (var optionLength = new DisposableIntPtr(IntPtr.Size))
            using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(int))))
            {
                Marshal.WriteInt32(optionLength, sizeof(int));

                int rc = RetryGetSocketOptionIfInterrupted(option, optionValue.Ptr, optionLength.Ptr);
                value = Marshal.ReadInt32(optionValue);

                return rc;
            }
        }

        public int GetSocketOption(int option, out long value)
        {
            using (var optionLength = new DisposableIntPtr(IntPtr.Size))
            using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(long))))
            {
                Marshal.WriteInt32(optionLength, sizeof(long));

                int rc = RetryGetSocketOptionIfInterrupted(option, optionValue.Ptr, optionLength.Ptr);
                value = Marshal.ReadInt64(optionValue);

                return rc;
            }
        }

        public int GetSocketOption(int option, out ulong value)
        {
            using (var optionLength = new DisposableIntPtr(IntPtr.Size))
            using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(ulong))))
            {
                Marshal.WriteInt32(optionLength, sizeof(ulong));

                int rc = RetryGetSocketOptionIfInterrupted(option, optionValue.Ptr, optionLength.Ptr);
                value = unchecked(Convert.ToUInt64(Marshal.ReadInt64(optionValue)));

                return rc;
            }
        }

        public int GetSocketOption(int option, out byte[] value)
        {
            using (var optionLength = new DisposableIntPtr(IntPtr.Size))
            using (var optionValue = new DisposableIntPtr(MaxBinaryOptionSize))
            {
                Marshal.WriteInt32(optionLength, MaxBinaryOptionSize);

                int rc = RetryGetSocketOptionIfInterrupted(option, optionValue.Ptr, optionLength.Ptr);

                value = new byte[Marshal.ReadInt32(optionLength)];
                Marshal.Copy(optionValue, value, 0, value.Length);

                return rc;
            }
        }

        public int GetSocketOption(int option, out string value)
        {
            using (var optionLength = new DisposableIntPtr(IntPtr.Size))
            using (var optionValue = new DisposableIntPtr(MaxBinaryOptionSize))
            {
                Marshal.WriteInt32(optionLength, MaxBinaryOptionSize);

                int rc = RetryGetSocketOptionIfInterrupted(option, optionValue.Ptr, optionLength.Ptr);

                value = rc == 0 ? Marshal.PtrToStringAnsi(optionValue) : string.Empty;

                return rc;
            }
        }

        public int SetSocketOption(int option, string value)
        {
            if (value == null)
            {
                return RetrySetSocketOptionIfInterrupted(option, IntPtr.Zero, 0);
            }

            var encoded = System.Text.Encoding.ASCII.GetBytes(value + "\x0");
            using (var optionValue = new DisposableIntPtr(encoded.Length))
            {
                Marshal.Copy(encoded, 0, optionValue, encoded.Length);

                return RetrySetSocketOptionIfInterrupted(option, optionValue.Ptr, value.Length);
            }
        }

        public int SetSocketOption(int option, int value)
        {
            using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(int))))
            {
                Marshal.WriteInt32(optionValue, value);

                return RetrySetSocketOptionIfInterrupted(option, optionValue.Ptr, sizeof(int));
            }
        }

        public int SetSocketOption(int option, long value)
        {
            using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(long))))
            {
                Marshal.WriteInt64(optionValue, value);

                return RetrySetSocketOptionIfInterrupted(option, optionValue.Ptr, sizeof(long));
            }
        }

        public int SetSocketOption(int option, ulong value)
        {
            using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(ulong))))
            {
                Marshal.WriteInt64(optionValue, unchecked(Convert.ToInt64(value)));

                return RetrySetSocketOptionIfInterrupted(option, optionValue.Ptr, sizeof(ulong));
            }
        }

        public int SetSocketOption(int option, byte[] value)
        {
            using (var optionValue = new DisposableIntPtr(value.Length))
            {
                Marshal.Copy(value, 0, optionValue, value.Length);

                return RetrySetSocketOptionIfInterrupted(option, optionValue.Ptr, value.Length);
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _msg.Dispose(disposing);

            if (!_disposed && disposing)
            {
                Marshal.FreeHGlobal(_buffer);

                Close();
            }

            _disposed = true;
        }

        private int GetReceiveMore(out int receiveMore)
        {
            if (LibZmq.MajorVersion >= 3)
            {
                return GetSocketOption((int)SocketOption.RCVMORE, out receiveMore);
            }

            long value;
            int rc = GetSocketOption((int)SocketOption.RCVMORE, out value);
            receiveMore = (int)value;

            return rc;
        }

        private int RetryGetSocketOptionIfInterrupted(int option, IntPtr optionValue, IntPtr optionLength)
        {
#if UNIX
          return Retry.IfInterrupted(LibZmq.zmq_getsockopt, SocketHandle, option, optionValue, optionLength);
#else
          return Retry.IfInterrupted(LibZmq.zmq_getsockopt.Invoke, SocketHandle, option, optionValue, optionLength);
#endif
        }

        private int RetrySetSocketOptionIfInterrupted(int option, IntPtr optionValue, int optionLength)
        {
#if UNIX
          return Retry.IfInterrupted(LibZmq.zmq_setsockopt, SocketHandle, option, optionValue, optionLength);
#else
          return Retry.IfInterrupted(LibZmq.zmq_setsockopt.Invoke, SocketHandle, option, optionValue, optionLength);
#endif
        }
    }
}
