namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.InteropServices;

    internal struct DisposableIntPtr : IDisposable
    {
        public IntPtr Ptr;

        public DisposableIntPtr(int size)
        {
            Ptr = Marshal.AllocHGlobal(size);
        }

        public static implicit operator IntPtr(DisposableIntPtr disposablePtr)
        {
            return disposablePtr.Ptr;
        }

        public void Dispose()
        {
            if (Ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Ptr);
                Ptr = IntPtr.Zero;
            }
        }
    }
}
