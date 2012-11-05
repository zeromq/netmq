namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorEventData
    {
        public int Event;
        public IntPtr Addr;
        public int Value;

        public string Address
        {
            get { return Marshal.PtrToStringAnsi(Addr); }
        }
    }
}