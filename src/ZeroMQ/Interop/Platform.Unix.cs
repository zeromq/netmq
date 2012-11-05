#if UNIX

namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.InteropServices;

    internal static partial class Platform
    {
        public const string LibSuffix = ".so";

        private const string KernelLib = "libdl.so";

        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 0x100;

        public static SafeLibraryHandle OpenHandle(string filename)
        {
            return dlopen(filename, RTLD_NOW | RTLD_GLOBAL);
        }

        public static IntPtr LoadProcedure(SafeLibraryHandle handle, string functionName)
        {
            return dlsym(handle, functionName);
        }

        public static bool ReleaseHandle(IntPtr handle)
        {
            return dlclose(handle) == 0;
        }

        public static Exception GetLastLibraryError()
        {
            return new DllNotFoundException(dlerror());
        }

        [DllImport(KernelLib)]
        private static extern SafeLibraryHandle dlopen(string filename, int flags);

        [DllImport(KernelLib)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(KernelLib)]
        private static extern string dlerror();

        [DllImport(KernelLib)]
        private static extern IntPtr dlsym(SafeLibraryHandle handle, string symbol);
    }
}

#endif