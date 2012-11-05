#if MONO

namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.InteropServices;

    internal static class LibZmq
    {
        public const string LibraryName = "libzmq";

        // From zmq.h (v3):
        // typedef struct {unsigned char _ [32];} zmq_msg_t;
        private static readonly int Zmq3MsgTSize = 32 * Marshal.SizeOf(typeof(byte));

        // From zmq.h (v2):
        // #define ZMQ_MAX_VSM_SIZE 30
        //
        // typedef struct
        // {
        //     void *content;
        //     unsigned char flags;
        //     unsigned char vsm_size;
        //     unsigned char vsm_data [ZMQ_MAX_VSM_SIZE];
        // } zmq_msg_t;
        private static readonly int ZmqMaxVsmSize = 30 * Marshal.SizeOf(typeof(byte));
        private static readonly int Zmq2MsgTSize = IntPtr.Size + (Marshal.SizeOf(typeof(byte)) * 2) + ZmqMaxVsmSize;

        public static readonly int ZmqMsgTSize;

        public static readonly int MajorVersion;
        public static readonly int MinorVersion;
        public static readonly int PatchVersion;

        public static long PollTimeoutRatio;

        static LibZmq()
        {
            AssignCurrentVersion(out MajorVersion, out MinorVersion, out PatchVersion);

            if (MajorVersion >= 3)
            {
                zmq_msg_recv = zmq_msg_recv_v3;
                zmq_msg_send = zmq_msg_send_v3;

                zmq_buffer_recv = zmq_recvbuf_v3;
                zmq_buffer_send = zmq_sendbuf_v3;

                zmq_msg_get = zmq_msg_get_v3;
                zmq_msg_init_data = zmq_msg_init_data_v3;
                zmq_msg_move = zmq_msg_move_v3;

                zmq_ctx_get = zmq_ctx_get_v3;
                zmq_ctx_set = zmq_ctx_set_v3;
                zmq_socket_monitor = zmq_socket_monitor_v3;

                zmq_unbind = zmq_unbind_v3;
                zmq_disconnect = zmq_disconnect_v3;

                PollTimeoutRatio = 1;
                ZmqMsgTSize = Zmq3MsgTSize;

                zmq_ctx_new = zmq_ctx_new_v3;
                zmq_ctx_destroy = zmq_ctx_destroy_v3;
            }
            else if (MajorVersion == 2)
            {
                zmq_msg_recv = (msg, sck, flags) => zmq_recvmsg(sck, msg, flags);
                zmq_msg_send = (msg, sck, flags) => zmq_sendmsg(sck, msg, flags);

                zmq_buffer_recv = null;
                zmq_buffer_send = null;

                zmq_msg_get = (message, option, optval, optvallen) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 1); };
                zmq_msg_init_data = (msg, data, size, ffn, hint) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 1); };
                zmq_msg_move = (destmsg, srcmsg) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 1); };

                zmq_ctx_get = (ctx, opt) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 2); };
                zmq_ctx_set = (ctx, opt, val) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 2); };
                zmq_socket_monitor = (sck, addr, e) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 2); };

                zmq_unbind = (sck, addr) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 2); };
                zmq_disconnect = (sck, addr) => { throw new ZmqVersionException(MajorVersion, MinorVersion, 3, 2); };

                PollTimeoutRatio = 1000;
                ZmqMsgTSize = Zmq2MsgTSize;

                zmq_ctx_new = () => zmq_init(1);
                zmq_ctx_destroy = zmq_term;
            }
        }

        private static void AssignCurrentVersion(out int majorVersion, out int minorVersion, out int patchVersion)
        {
            int sizeofInt32 = Marshal.SizeOf(typeof(int));

            IntPtr majorPointer = Marshal.AllocHGlobal(sizeofInt32);
            IntPtr minorPointer = Marshal.AllocHGlobal(sizeofInt32);
            IntPtr patchPointer = Marshal.AllocHGlobal(sizeofInt32);

            zmq_version(majorPointer, minorPointer, patchPointer);

            majorVersion = Marshal.ReadInt32(majorPointer);
            minorVersion = Marshal.ReadInt32(minorPointer);
            patchVersion = Marshal.ReadInt32(patchPointer);

            Marshal.FreeHGlobal(majorPointer);
            Marshal.FreeHGlobal(minorPointer);
            Marshal.FreeHGlobal(patchPointer);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FreeMessageDataCallback(IntPtr data, IntPtr hint);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCtxGetProc(IntPtr context, int option);
        public static ZmqCtxGetProc zmq_ctx_get;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCtxSetProc(IntPtr context, int option, int optval);
        public static ZmqCtxSetProc zmq_ctx_set;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqSocketMonitorProc(IntPtr socket, string addr, int events);
        public static ZmqSocketMonitorProc zmq_socket_monitor;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate int ZmqBindProc(IntPtr socket, string addr);
        public static ZmqBindProc zmq_unbind;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate int ZmqConnectProc(IntPtr socket, string addr);
        public static ZmqConnectProc zmq_disconnect;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqGetMsgOptProc(IntPtr message, int option, IntPtr optval, IntPtr optvallen);
        public static readonly ZmqGetMsgOptProc zmq_msg_get;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqRecvMsgProc(IntPtr msg, IntPtr socket, int flags);
        public static readonly ZmqRecvMsgProc zmq_msg_recv;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqBufferRecvProc(IntPtr socket, IntPtr buf, int size, int flags);
        public static ZmqBufferRecvProc zmq_buffer_recv;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqSendMsgProc(IntPtr msg, IntPtr socket, int flags);
        public static readonly ZmqSendMsgProc zmq_msg_send;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqBufferSendProc(IntPtr socket, IntPtr buf, int size, int flags);
        public static ZmqBufferSendProc zmq_buffer_send;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgInitDataProc(IntPtr msg, IntPtr data, int size, FreeMessageDataCallback ffn, IntPtr hint);
        public static readonly ZmqMsgInitDataProc zmq_msg_init_data;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgMoveProc(IntPtr destmsg, IntPtr srcmsg);
        public static readonly ZmqMsgMoveProc zmq_msg_move;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ZmqCtxNewProc();
        public static readonly ZmqCtxNewProc zmq_ctx_new;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCtxDestroyProc(IntPtr context);
        public static readonly ZmqCtxDestroyProc zmq_ctx_destroy;

        [DllImport(LibraryName, EntryPoint = "zmq_ctx_new", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr zmq_ctx_new_v3();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr zmq_init(int io_threads);

        [DllImport(LibraryName, EntryPoint = "zmq_ctx_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_ctx_destroy_v3(IntPtr context);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_term(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "zmq_ctx_get", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_ctx_get_v3(IntPtr context, int option);

        [DllImport(LibraryName, EntryPoint = "zmq_ctx_set", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_ctx_set_v3(IntPtr context, int option, int optval);

        [DllImport(LibraryName, EntryPoint = "zmq_socket_monitor", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_socket_monitor_v3(IntPtr socket, string addr, int events);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_close(IntPtr socket);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_setsockopt(IntPtr socket, int option, IntPtr optval, int optvallen);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_getsockopt(IntPtr socket, int option, IntPtr optval, IntPtr optvallen);

        [DllImport(LibraryName, EntryPoint = "zmq_msg_get", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_get_v3(IntPtr message, int option, IntPtr optval, IntPtr optvallen);

        [DllImport(LibraryName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_bind(IntPtr socket, string addr);

        [DllImport(LibraryName, EntryPoint = "zmq_unbind", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_unbind_v3(IntPtr socket, string addr);

        [DllImport(LibraryName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_connect(IntPtr socket, string addr);

        [DllImport(LibraryName, EntryPoint = "zmq_disconnect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_disconnect_v3(IntPtr socket, string addr);

        [DllImport(LibraryName, EntryPoint = "zmq_recv", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_recvmsg(IntPtr socket, IntPtr msg, int flags);

        [DllImport(LibraryName, EntryPoint = "zmq_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_sendmsg(IntPtr socket, IntPtr msg, int flags);

        [DllImport(LibraryName, EntryPoint = "zmq_msg_recv", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_recv_v3(IntPtr msg, IntPtr socket, int flags);

        [DllImport(LibraryName, EntryPoint = "zmq_msg_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_send_v3(IntPtr msg, IntPtr socket, int flags);

        [DllImport(LibraryName, EntryPoint = "zmq_recv", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_recvbuf_v3(IntPtr socket, IntPtr buf, int size, int flags);

        [DllImport(LibraryName, EntryPoint = "zmq_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_sendbuf_v3(IntPtr socket, IntPtr buf, int size, int flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr zmq_socket(IntPtr context, int type);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_close(IntPtr msg);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_copy(IntPtr destmsg, IntPtr srcmsg);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr zmq_msg_data(IntPtr msg);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_init(IntPtr msg);

        [DllImport(LibraryName, EntryPoint = "zmq_msg_init_data", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_init_data_v3(IntPtr msg, IntPtr data, int size, FreeMessageDataCallback ffn, IntPtr hint);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_init_size(IntPtr msg, int size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_size(IntPtr msg);

        [DllImport(LibraryName, EntryPoint = "zmq_msg_move", CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_msg_move_v3(IntPtr destmsg, IntPtr srcmsg);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_errno();

        [DllImport(LibraryName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr zmq_strerror(int errnum);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_device(int device, IntPtr inSocket, IntPtr outSocket);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void zmq_version(IntPtr major, IntPtr minor, IntPtr patch);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int zmq_poll([In, Out] PollItem[] items, int numItems, long timeout);
    }
}

#endif