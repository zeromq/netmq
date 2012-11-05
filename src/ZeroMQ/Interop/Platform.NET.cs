#if !MONO

namespace ZeroMQ.Interop
{
    internal static partial class Platform
    {
        internal static class Errno
        {
            // ReSharper disable InconsistentNaming
            public const int EPERM = 1;
            public const int ENOENT = 2;
            public const int ESRCH = 3;
            public const int EINTR = 4;
            public const int EIO = 5;
            public const int ENXIO = 6;
            public const int E2BIG = 7;
            public const int ENOEXEC = 8;
            public const int EBADF = 9;
            public const int ECHILD = 10;
            public const int EAGAIN = 11;
            public const int ENOMEM = 12;
            public const int EACCES = 13;
            public const int EFAULT = 14;
            public const int EBUSY = 16;
            public const int EEXIST = 17;
            public const int EXDEV = 18;
            public const int ENODEV = 19;
            public const int ENOTDIR = 20;
            public const int EISDIR = 21;
            public const int EINVAL = 22;
            public const int ENFILE = 23;
            public const int EMFILE = 24;
            public const int ENOTTY = 25;
            public const int EFBIG = 27;
            public const int ENOSPC = 28;
            public const int ESPIPE = 29;
            public const int EROFS = 30;
            public const int EMLINK = 31;
            public const int EPIPE = 32;
            public const int EDOM = 33;
            public const int EDEADLK = 36;
            public const int ENAMETOOLONG = 38;
            public const int ENOLCK = 39;
            public const int ENOSYS = 40;
            public const int ENOTEMPTY = 41;
            public const int EADDRINUSE = 100;
            public const int EADDRNOTAVAIL = 101;
            public const int ECONNREFUSED = 107;
            public const int EINPROGRESS = 112;
            public const int ENETDOWN = 116;
            public const int ENOBUFS = 119;
            public const int ENOTSOCK = 128;
            public const int ENOTSUP = 129;
            public const int EPROTONOSUPPORT = 135;
            // ReSharper restore InconsistentNaming
        }
    }
}

#endif