namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PollItem : IEquatable<PollItem>
    {
        public IntPtr Socket;
#if UNIX
        public int FileDescriptor;
#else
        public IntPtr FileDescriptor;
#endif
        public short Events;
        public short ReadyEvents;

        public PollItem(IntPtr socket, PollEvents pollEvents)
        {
            if (socket == IntPtr.Zero)
            {
                throw new ArgumentException("Expected a valid socket handle.", "socket");
            }

            Socket = socket;
#if UNIX
            FileDescriptor = 0;
#else
            FileDescriptor = IntPtr.Zero;
#endif
            Events = (short)pollEvents;
            ReadyEvents = 0;
        }

        public bool Equals(PollItem other)
        {
            return Socket == other.Socket;
        }

        public override int GetHashCode()
        {
// ReSharper disable NonReadonlyFieldInGetHashCode -- 'Socket' will be modified by unmanaged code
            return Socket.GetHashCode();
// ReSharper restore NonReadonlyFieldInGetHashCode
        }
    }
}
