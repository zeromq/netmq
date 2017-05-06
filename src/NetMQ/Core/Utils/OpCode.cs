#if !NETSTANDARD1_3 && !UAP
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace NetMQ.Core.Utils
{
    internal static class Opcode
    {
        private static IntPtr s_codeBuffer;
        private static ulong s_size;

        public static bool Open()
        {
            var p = (int)Environment.OSVersion.Platform;

            byte[] rdtscCode = IntPtr.Size == 4 ? RDTSC_32 : RDTSC_64;

            s_size = (ulong)(rdtscCode.Length);

            if ((p == 4) || (p == 128))
            {
                // Unix
                if (IsARMArchitecture()) return false;

                Assembly assembly = Assembly.Load("Mono.Posix");

                Type syscall = assembly.GetType("Mono.Unix.Native.Syscall");
                MethodInfo mmap = syscall.GetMethod("mmap");

                Type mmapProts = assembly.GetType("Mono.Unix.Native.MmapProts");
                object mmapProtsParam = Enum.ToObject(mmapProts,
                    (int)mmapProts.GetField("PROT_READ").GetValue(null) |
                    (int)mmapProts.GetField("PROT_WRITE").GetValue(null) |
                    (int)mmapProts.GetField("PROT_EXEC").GetValue(null));

                Type mmapFlags = assembly.GetType("Mono.Unix.Native.MmapFlags");
                object mmapFlagsParam = Enum.ToObject(mmapFlags,
                    (int)mmapFlags.GetField("MAP_ANONYMOUS").GetValue(null) |
                    (int)mmapFlags.GetField("MAP_PRIVATE").GetValue(null));

                s_codeBuffer = (IntPtr)mmap.Invoke(null,
                    new[] { IntPtr.Zero, s_size, mmapProtsParam, mmapFlagsParam, -1, 0 });

                if (s_codeBuffer == IntPtr.Zero || s_codeBuffer == (IntPtr)(-1))
                {
                    throw new InvalidOperationException("Mmap failed");
                }
            }
            else
            {
                // Windows
                s_codeBuffer = NativeMethods.VirtualAlloc(IntPtr.Zero,
                    (UIntPtr)s_size, AllocationType.COMMIT | AllocationType.RESERVE,
                    MemoryProtection.EXECUTE_READWRITE);
            }

            Marshal.Copy(rdtscCode, 0, s_codeBuffer, rdtscCode.Length);

            Rdtsc = Marshal.GetDelegateForFunctionPointer(s_codeBuffer, typeof(RdtscDelegate)) as RdtscDelegate;

            return true;
        }

        private static bool IsARMArchitecture()
        {
            // force to load from mono gac
            Assembly currentAssembly = Assembly.Load("Mono.Posix, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
            Type syscall = currentAssembly.GetType("Mono.Unix.Native.Syscall");
            Type utsname = currentAssembly.GetType("Mono.Unix.Native.Utsname");
            MethodInfo uname = syscall.GetMethod("uname");
            object[] parameters = { null };

            var invokeResult = (int)uname.Invoke(null, parameters);

            if (invokeResult != 0)
                return false;

            var currentValues = parameters[0];
            var machineValue = (string)utsname.GetField("machine").GetValue(currentValues);
            return machineValue.ToLower().Contains("arm");
        }

        public static void Close()
        {
            Rdtsc = null;

            var p = (int)Environment.OSVersion.Platform;
            if ((p == 4) || (p == 128))
            {
                // Unix
                Assembly assembly =
                    Assembly.Load("Mono.Posix, Version=2.0.0.0, Culture=neutral, " +
                    "PublicKeyToken=0738eb9f132ed756");

                Type syscall = assembly.GetType("Mono.Unix.Native.Syscall");
                MethodInfo munmap = syscall.GetMethod("munmap");
                munmap.Invoke(null, new object[] { s_codeBuffer, s_size });
            }
            else
            {
                // Windows
                NativeMethods.VirtualFree(s_codeBuffer, UIntPtr.Zero, FreeType.RELEASE);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate ulong RdtscDelegate();

        [CanBeNull]
        public static RdtscDelegate Rdtsc { get; private set; }

        // unsigned __int64 __stdcall rdtsc() {
        //   return __rdtsc();
        // }

        private static readonly byte[] RDTSC_32 = {
            0x0F, 0x31,                     // rdtsc
            0xC3                            // ret
        };

        private static readonly byte[] RDTSC_64 = {
            0x0F, 0x31,                     // rdtsc
            0x48, 0xC1, 0xE2, 0x20,         // shl rdx, 20h
            0x48, 0x0B, 0xC2,               // or rax, rdx
            0xC3                            // ret
        };

        [Flags]
        public enum AllocationType : uint
        {
            COMMIT = 0x1000,
            RESERVE = 0x2000,
            RESET = 0x80000,
            LARGE_PAGES = 0x20000000,
            PHYSICAL = 0x400000,
            TOP_DOWN = 0x100000,
            WRITE_WATCH = 0x200000
        }

        [Flags]
        public enum MemoryProtection : uint
        {
            EXECUTE = 0x10,
            EXECUTE_READ = 0x20,
            EXECUTE_READWRITE = 0x40,
            EXECUTE_WRITECOPY = 0x80,
            NOACCESS = 0x01,
            READONLY = 0x02,
            READWRITE = 0x04,
            WRITECOPY = 0x08,
            GUARD = 0x100,
            NOCACHE = 0x200,
            WRITECOMBINE = 0x400
        }

        [Flags]
        public enum FreeType
        {
            DECOMMIT = 0x4000,
            RELEASE = 0x8000
        }

        private static class NativeMethods
        {
            private const string Kernel = "kernel32.dll";

            [DllImport(Kernel, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
                AllocationType flAllocationType, MemoryProtection flProtect);

            [DllImport(Kernel, CallingConvention = CallingConvention.Winapi)]
            public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize,
                FreeType dwFreeType);
        }
    }
}
#endif