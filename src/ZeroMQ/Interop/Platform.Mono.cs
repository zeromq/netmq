#if MONO

namespace ZeroMQ.Interop
{
    using Mono.Unix.Native;
    using NativeErrno=Mono.Unix.Native.Errno;

    internal static partial class Platform
    {
        internal static class Errno
        {
            public static readonly int EPERM = NativeConvert.FromErrno(NativeErrno.EPERM);
            public static readonly int ENOENT = NativeConvert.FromErrno(NativeErrno.ENOENT);
            public static readonly int ESRCH = NativeConvert.FromErrno(NativeErrno.ESRCH);
            public static readonly int EINTR = NativeConvert.FromErrno(NativeErrno.EINTR);
            public static readonly int EIO = NativeConvert.FromErrno(NativeErrno.EIO);
            public static readonly int ENXIO = NativeConvert.FromErrno(NativeErrno.ENXIO);
            public static readonly int E2BIG = NativeConvert.FromErrno(NativeErrno.E2BIG);
            public static readonly int ENOEXEC = NativeConvert.FromErrno(NativeErrno.ENOEXEC);
            public static readonly int EBADF = NativeConvert.FromErrno(NativeErrno.EBADF);
            public static readonly int ECHILD = NativeConvert.FromErrno(NativeErrno.ECHILD);
            public static readonly int EAGAIN = NativeConvert.FromErrno(NativeErrno.EAGAIN);
            public static readonly int ENOMEM = NativeConvert.FromErrno(NativeErrno.ENOMEM);
            public static readonly int EACCES = NativeConvert.FromErrno(NativeErrno.EACCES);
            public static readonly int EFAULT = NativeConvert.FromErrno(NativeErrno.EFAULT);
            public static readonly int EBUSY = NativeConvert.FromErrno(NativeErrno.EBUSY);
            public static readonly int EEXIST = NativeConvert.FromErrno(NativeErrno.EEXIST);
            public static readonly int EXDEV = NativeConvert.FromErrno(NativeErrno.EXDEV);
            public static readonly int ENODEV = NativeConvert.FromErrno(NativeErrno.ENODEV);
            public static readonly int ENOTDIR = NativeConvert.FromErrno(NativeErrno.ENOTDIR);
            public static readonly int EISDIR = NativeConvert.FromErrno(NativeErrno.EISDIR);
            public static readonly int EINVAL = NativeConvert.FromErrno(NativeErrno.EINVAL);
            public static readonly int ENFILE = NativeConvert.FromErrno(NativeErrno.ENFILE);
            public static readonly int EMFILE = NativeConvert.FromErrno(NativeErrno.EMFILE);
            public static readonly int ENOTTY = NativeConvert.FromErrno(NativeErrno.ENOTTY);
            public static readonly int EFBIG = NativeConvert.FromErrno(NativeErrno.EFBIG);
            public static readonly int ENOSPC = NativeConvert.FromErrno(NativeErrno.ENOSPC);
            public static readonly int ESPIPE = NativeConvert.FromErrno(NativeErrno.ESPIPE);
            public static readonly int EROFS = NativeConvert.FromErrno(NativeErrno.EROFS);
            public static readonly int EMLINK = NativeConvert.FromErrno(NativeErrno.EMLINK);
            public static readonly int EPIPE = NativeConvert.FromErrno(NativeErrno.EPIPE);
            public static readonly int EDOM = NativeConvert.FromErrno(NativeErrno.EDOM);
            public static readonly int EDEADLK = NativeConvert.FromErrno(NativeErrno.EDEADLK);
            public static readonly int ENAMETOOLONG = NativeConvert.FromErrno(NativeErrno.ENAMETOOLONG);
            public static readonly int ENOLCK = NativeConvert.FromErrno(NativeErrno.ENOLCK);
            public static readonly int ENOSYS = NativeConvert.FromErrno(NativeErrno.ENOSYS);
            public static readonly int ENOTEMPTY = NativeConvert.FromErrno(NativeErrno.ENOTEMPTY);
            public static readonly int EADDRINUSE = NativeConvert.FromErrno(NativeErrno.EADDRINUSE);
            public static readonly int EADDRNOTAVAIL = NativeConvert.FromErrno(NativeErrno.EADDRNOTAVAIL);
            public static readonly int ECONNREFUSED = NativeConvert.FromErrno(NativeErrno.ECONNREFUSED);
            public static readonly int EINPROGRESS = NativeConvert.FromErrno(NativeErrno.EINPROGRESS);
            public static readonly int ENETDOWN = NativeConvert.FromErrno(NativeErrno.ENETDOWN);
            public static readonly int ENOBUFS = NativeConvert.FromErrno(NativeErrno.ENOBUFS);
            public static readonly int ENOTSOCK = NativeConvert.FromErrno(NativeErrno.ENOTSOCK);
            public static readonly int ENOTSUP = NativeConvert.FromErrno(NativeErrno.EOPNOTSUPP);
            public static readonly int EPROTONOSUPPORT = NativeConvert.FromErrno(NativeErrno.EPROTONOSUPPORT);
        }
    }
}

#endif