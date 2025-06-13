using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using AsyncIO;
using NetMQ.Core.Transports;

namespace NetMQ.Core
{
    internal class MonitorEvent
    {
        private const int ValueInteger = 1;
        private const int ValueChannel = 2;

        private readonly SocketEvents m_monitorEvent;
        private readonly string m_addr;
        private readonly object? m_arg;
        private readonly int m_flag;

        private static readonly int s_sizeOfIntPtr;

        static MonitorEvent()
        {
            s_sizeOfIntPtr = Marshal.SizeOf(typeof(IntPtr));

            if (s_sizeOfIntPtr > 4)
                s_sizeOfIntPtr = 8;
        }

        public MonitorEvent(SocketEvents monitorEvent, string addr, ErrorCode arg)
            : this(monitorEvent, addr, (int)arg)
        {
        }

        public MonitorEvent(SocketEvents monitorEvent, string addr, int arg)
            : this(monitorEvent, addr, (object)arg)
        {
        }

        public MonitorEvent(SocketEvents monitorEvent, string addr, AsyncSocket arg)
            : this(monitorEvent, addr, (object)arg)
        {
        }

        private MonitorEvent(SocketEvents monitorEvent, string addr, object? arg)
        {
            m_monitorEvent = monitorEvent;
            m_addr = addr;
            m_arg = arg;

            if (arg is int)
                m_flag = ValueInteger;
            else if (arg is AsyncSocket)
                m_flag = ValueChannel;
            else
                m_flag = 0;
        }

        public string Addr => m_addr;

        public object? Arg => m_arg;

        public SocketEvents Event => m_monitorEvent;

        public void Write(SocketBase s)
        {
            int size = 4 + 1 + (m_addr?.Length ?? 0) + 1; // event + len(addr) + addr + flag

            if (m_flag == ValueInteger)
                size += 4;
            else if (m_flag == ValueChannel)
                size += s_sizeOfIntPtr;

            int pos = 0;

            ByteArraySegment buffer = new byte[size];
            buffer.PutInteger(Endianness.Little, (int)m_monitorEvent, pos);
            pos += 4;

            if (m_addr != null)
            {
                buffer[pos++] = (byte)m_addr.Length;

                // was not here originally

                buffer.PutString(m_addr, pos);
                pos += m_addr.Length;
            }
            else
                buffer[pos++] = 0;

            buffer[pos++] = ((byte)m_flag);
            if (m_flag == ValueInteger)
            {
                buffer.PutInteger(Endianness.Little, (int)m_arg!, pos);
            }
            else if (m_flag == ValueChannel)
            {
                GCHandle handle = GCHandle.Alloc(m_arg, GCHandleType.Weak);

                if (s_sizeOfIntPtr == 4)
                    buffer.PutInteger(Endianness.Little, GCHandle.ToIntPtr(handle).ToInt32(), pos);
                else
                    buffer.PutLong(Endianness.Little, GCHandle.ToIntPtr(handle).ToInt64(), pos);
            }

            var msg = new Msg();
            msg.InitGC((byte[])buffer, buffer.Size);
            // An infinite timeout here can cause the IO thread to hang
            // see https://github.com/zeromq/netmq/issues/539
            s.TrySend(ref msg, TimeSpan.Zero, false);
        }

        public static MonitorEvent Read(SocketBase s)
        {
            var msg = new Msg();
            msg.InitEmpty();

            s.TryRecv(ref msg, SendReceiveConstants.InfiniteTimeout);

            int pos = msg.UnsafeOffset;

            Assumes.NotNull(msg.UnsafeData);

            ByteArraySegment data = msg.UnsafeData;

            var @event = (SocketEvents)data.GetInteger(Endianness.Little, pos);
            pos += 4;
            var len = (int)data[pos++];
            string addr = data.GetString(len, pos);
            pos += len;
            var flag = (int)data[pos++];
            object? arg = null;

            if (flag == ValueInteger)
            {
                arg = data.GetInteger(Endianness.Little, pos);
            }
            else if (flag == ValueChannel)
            {
                IntPtr value = s_sizeOfIntPtr == 4
                    ? new IntPtr(data.GetInteger(Endianness.Little, pos))
                    : new IntPtr(data.GetLong(Endianness.Little, pos));

                GCHandle handle = GCHandle.FromIntPtr(value);
                AsyncSocket? socket = null;

                if (handle.IsAllocated)
                {
                    socket = handle.Target as AsyncSocket;
                }

                handle.Free();

                arg = socket;
            }

            return new MonitorEvent(@event, addr, arg);
        }

        [return: MaybeNull]
        public T ConvertArg<T>()
        {
            if (Arg is T v)
                return v;

            if (Arg is null && default(T) is null)
                return default;

            throw new ArgumentException($"Command argument must be of type {typeof(T).Name}.");
        }
    }
}