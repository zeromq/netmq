#if !MONO

namespace ZeroMQ.Interop
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;

    // ReSharper disable InconsistentNaming
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Compatibility with native headers.")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Compatibility with native headers.")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Compatibility with native headers.")]
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:ElementsMustAppearInTheCorrectOrder", Justification = "Compatibility with native headers.")]
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Reviewed. Suppression is OK here.")]
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

        private static readonly UnmanagedLibrary NativeLib;

        public static readonly long PollTimeoutRatio;

        static LibZmq()
        {
            NativeLib = new UnmanagedLibrary(LibraryName);

            AssignCommonDelegates();
            AssignCurrentVersion(out MajorVersion, out MinorVersion, out PatchVersion);

            if (MajorVersion >= 3)
            {
                zmq_msg_get = NativeLib.GetUnmanagedFunction<ZmqMsgGetProc>("zmq_msg_get");
                zmq_msg_init_data = NativeLib.GetUnmanagedFunction<ZmqMsgInitDataProc>("zmq_msg_init_data");
                zmq_msg_move = NativeLib.GetUnmanagedFunction<ZmqMsgMoveProc>("zmq_msg_move");

                zmq_msg_recv = NativeLib.GetUnmanagedFunction<ZmqMsgRecvProc>("zmq_msg_recv");
                zmq_msg_send = NativeLib.GetUnmanagedFunction<ZmqMsgSendProc>("zmq_msg_send");

                zmq_buffer_recv = NativeLib.GetUnmanagedFunction<ZmqBufferRecvProc>("zmq_recv");
                zmq_buffer_send = NativeLib.GetUnmanagedFunction<ZmqBufferSendProc>("zmq_send");

                zmq_ctx_new = NativeLib.GetUnmanagedFunction<ZmqCtxNewProc>("zmq_ctx_new");
                zmq_ctx_destroy = NativeLib.GetUnmanagedFunction<ZmqCtxDestroyProc>("zmq_ctx_destroy");
                zmq_ctx_get = NativeLib.GetUnmanagedFunction<ZmqCtxGetProc>("zmq_ctx_get");
                zmq_ctx_set = NativeLib.GetUnmanagedFunction<ZmqCtxSetProc>("zmq_ctx_set");
                zmq_socket_monitor = NativeLib.GetUnmanagedFunction<ZmqSocketMonitorProc>("zmq_socket_monitor");

                zmq_unbind = NativeLib.GetUnmanagedFunction<ZmqBindProc>("zmq_unbind");
                zmq_disconnect = NativeLib.GetUnmanagedFunction<ZmqConnectProc>("zmq_disconnect");

                PollTimeoutRatio = 1;
                ZmqMsgTSize = Zmq3MsgTSize;
            }
            else if (MajorVersion == 2)
            {
                var zmq_msg_recv_impl = NativeLib.GetUnmanagedFunction<ZmqMsgRecvProc>("zmq_recv");
                var zmq_msg_send_impl = NativeLib.GetUnmanagedFunction<ZmqMsgSendProc>("zmq_send");
                zmq_msg_recv = (msg, sck, flags) => zmq_msg_recv_impl(sck, msg, flags);
                zmq_msg_send = (msg, sck, flags) => zmq_msg_send_impl(sck, msg, flags);

                zmq_buffer_recv = null;
                zmq_buffer_send = null;

                var zmq_init = NativeLib.GetUnmanagedFunction<ZmqInitProc>("zmq_init");
                zmq_ctx_new = () => zmq_init(1);
                zmq_ctx_destroy = NativeLib.GetUnmanagedFunction<ZmqCtxDestroyProc>("zmq_term");

                PollTimeoutRatio = 1000;
                ZmqMsgTSize = Zmq2MsgTSize;
            }
        }

        private static void AssignCommonDelegates()
        {
            zmq_close = NativeLib.GetUnmanagedFunction<ZmqCloseProc>("zmq_close");
            zmq_setsockopt = NativeLib.GetUnmanagedFunction<ZmqSetSockOptProc>("zmq_setsockopt");
            zmq_getsockopt = NativeLib.GetUnmanagedFunction<ZmqGetSockOptProc>("zmq_getsockopt");
            zmq_bind = NativeLib.GetUnmanagedFunction<ZmqBindProc>("zmq_bind");
            zmq_connect = NativeLib.GetUnmanagedFunction<ZmqConnectProc>("zmq_connect");
            zmq_socket = NativeLib.GetUnmanagedFunction<ZmqSocketProc>("zmq_socket");
            zmq_msg_close = NativeLib.GetUnmanagedFunction<ZmqMsgCloseProc>("zmq_msg_close");
            zmq_msg_copy = NativeLib.GetUnmanagedFunction<ZmqMsgCopyProc>("zmq_msg_copy");
            zmq_msg_data = NativeLib.GetUnmanagedFunction<ZmqMsgDataProc>("zmq_msg_data");
            zmq_msg_init = NativeLib.GetUnmanagedFunction<ZmqMsgInitProc>("zmq_msg_init");
            zmq_msg_init_size = NativeLib.GetUnmanagedFunction<ZmqMsgInitSizeProc>("zmq_msg_init_size");
            zmq_msg_size = NativeLib.GetUnmanagedFunction<ZmqMsgSizeProc>("zmq_msg_size");
            zmq_errno = NativeLib.GetUnmanagedFunction<ZmqErrnoProc>("zmq_errno");
            zmq_strerror = NativeLib.GetUnmanagedFunction<ZmqStrErrorProc>("zmq_strerror");
            zmq_version = NativeLib.GetUnmanagedFunction<ZmqVersionProc>("zmq_version");
            zmq_poll = NativeLib.GetUnmanagedFunction<ZmqPollProc>("zmq_poll");
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
        public delegate void MonitorFuncCallback(IntPtr socket, int eventFlags, ref MonitorEventData data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ZmqCtxNewProc();
        public static ZmqCtxNewProc zmq_ctx_new;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ZmqInitProc(int io_threads);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCtxDestroyProc(IntPtr context);
        public static ZmqCtxDestroyProc zmq_ctx_destroy;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCtxGetProc(IntPtr context, int option);
        public static ZmqCtxGetProc zmq_ctx_get;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCtxSetProc(IntPtr context, int option, int optval);
        public static ZmqCtxSetProc zmq_ctx_set;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqSocketMonitorProc(IntPtr socket, string addr, int events);
        public static ZmqSocketMonitorProc zmq_socket_monitor;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqSetSockOptProc(IntPtr socket, int option, IntPtr optval, int optvallen);
        public static ZmqSetSockOptProc zmq_setsockopt;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqGetSockOptProc(IntPtr socket, int option, IntPtr optval, IntPtr optvallen);
        public static ZmqGetSockOptProc zmq_getsockopt;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgGetProc(IntPtr message, int option, IntPtr optval, IntPtr optvallen);
        public static ZmqMsgGetProc zmq_msg_get;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate int ZmqBindProc(IntPtr socket, string addr);
        public static ZmqBindProc zmq_bind;
        public static ZmqBindProc zmq_unbind;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate int ZmqConnectProc(IntPtr socket, string addr);
        public static ZmqConnectProc zmq_connect;
        public static ZmqConnectProc zmq_disconnect;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqCloseProc(IntPtr socket);
        public static ZmqCloseProc zmq_close;

        // NOTE: For 2.x, this method signature is (socket, msg, flags)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgRecvProc(IntPtr msg, IntPtr socket, int flags);
        public static ZmqMsgRecvProc zmq_msg_recv;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqBufferRecvProc(IntPtr socket, IntPtr buf, int size, int flags);
        public static ZmqBufferRecvProc zmq_buffer_recv;

        // NOTE: For 2.x, this method signature is (socket, msg, flags)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgSendProc(IntPtr msg, IntPtr socket, int flags);
        public static ZmqMsgSendProc zmq_msg_send;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqBufferSendProc(IntPtr socket, IntPtr buf, int size, int flags);
        public static ZmqBufferSendProc zmq_buffer_send;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ZmqSocketProc(IntPtr context, int type);
        public static ZmqSocketProc zmq_socket;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgCloseProc(IntPtr msg);
        public static ZmqMsgCloseProc zmq_msg_close;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgCopyProc(IntPtr destmsg, IntPtr srcmsg);
        public static ZmqMsgCopyProc zmq_msg_copy;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ZmqMsgDataProc(IntPtr msg);
        public static ZmqMsgDataProc zmq_msg_data;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgInitProc(IntPtr msg);
        public static ZmqMsgInitProc zmq_msg_init;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgInitSizeProc(IntPtr msg, int size);
        public static ZmqMsgInitSizeProc zmq_msg_init_size;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgInitDataProc(IntPtr msg, IntPtr data, int size, FreeMessageDataCallback ffn, IntPtr hint);
        public static ZmqMsgInitDataProc zmq_msg_init_data;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgMoveProc(IntPtr destmsg, IntPtr srcmsg);
        public static ZmqMsgMoveProc zmq_msg_move;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqMsgSizeProc(IntPtr msg);
        public static ZmqMsgSizeProc zmq_msg_size;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqErrnoProc();
        public static ZmqErrnoProc zmq_errno;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate IntPtr ZmqStrErrorProc(int errnum);
        public static ZmqStrErrorProc zmq_strerror;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ZmqVersionProc(IntPtr major, IntPtr minor, IntPtr patch);
        public static ZmqVersionProc zmq_version;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ZmqPollProc([In] [Out] PollItem[] items, int numItems, long timeoutMsec);
        public static ZmqPollProc zmq_poll;
    }
    // ReSharper restore InconsistentNaming
}

#endif