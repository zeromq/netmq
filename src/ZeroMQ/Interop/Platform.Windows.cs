#if !UNIX

namespace ZeroMQ.Interop
{
    using System;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;

    internal static partial class Platform
    {
        public const string LibSuffix = ".dll";

        private const string KernelLib = "kernel32";

        public static SafeLibraryHandle OpenHandle(string filename)
        {
            return LoadLibrary(filename);
        }

        public static IntPtr LoadProcedure(SafeLibraryHandle handle, string functionName)
        {
            return GetProcAddress(handle, functionName);
        }

        public static bool ReleaseHandle(IntPtr handle)
        {
            return FreeLibrary(handle);
        }

        public static Exception GetLastLibraryError()
        {
            return Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        [DllImport(KernelLib, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
        private static extern SafeLibraryHandle LoadLibrary(string fileName);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport(KernelLib, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr moduleHandle);

        [DllImport(KernelLib)]
        private static extern IntPtr GetProcAddress(SafeLibraryHandle moduleHandle, string procname);
    }
}

#endif