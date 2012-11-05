#pragma warning disable 1591

namespace ZeroMQ
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;    

    /// <summary>
    /// Contains cross-platform error code definitions.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented",
        Justification = "Errno values can be looked up easily.")]
    public static class ErrorCode
    {
        // ReSharper disable InconsistentNaming
        public static readonly int EPERM = Platform.Errno.EPERM;
        public static readonly int ENOENT = Platform.Errno.ENOENT;
        public static readonly int ESRCH = Platform.Errno.ESRCH;
        public static readonly int EINTR = Platform.Errno.EINTR;
        public static readonly int EIO = Platform.Errno.EIO;
        public static readonly int ENXIO = Platform.Errno.ENXIO;
        public static readonly int E2BIG = Platform.Errno.E2BIG;
        public static readonly int ENOEXEC = Platform.Errno.ENOEXEC;
        public static readonly int EBADF = Platform.Errno.EBADF;
        public static readonly int ECHILD = Platform.Errno.ECHILD;
        public static readonly int EAGAIN = Platform.Errno.EAGAIN;
        public static readonly int ENOMEM = Platform.Errno.ENOMEM;
        public static readonly int EACCES = Platform.Errno.EACCES;
        public static readonly int EFAULT = Platform.Errno.EFAULT;
        public static readonly int EBUSY = Platform.Errno.EBUSY;
        public static readonly int EEXIST = Platform.Errno.EEXIST;
        public static readonly int EXDEV = Platform.Errno.EXDEV;
        public static readonly int ENODEV = Platform.Errno.ENODEV;
        public static readonly int ENOTDIR = Platform.Errno.ENOTDIR;
        public static readonly int EISDIR = Platform.Errno.EISDIR;
        public static readonly int EINVAL = Platform.Errno.EINVAL;
        public static readonly int ENFILE = Platform.Errno.ENFILE;
        public static readonly int EMFILE = Platform.Errno.EMFILE;
        public static readonly int ENOTTY = Platform.Errno.ENOTTY;
        public static readonly int EFBIG = Platform.Errno.EFBIG;
        public static readonly int ENOSPC = Platform.Errno.ENOSPC;
        public static readonly int ESPIPE = Platform.Errno.ESPIPE;
        public static readonly int EROFS = Platform.Errno.EROFS;
        public static readonly int EMLINK = Platform.Errno.EMLINK;
        public static readonly int EPIPE = Platform.Errno.EPIPE;
        public static readonly int EDOM = Platform.Errno.EDOM;
        public static readonly int EDEADLK = Platform.Errno.EDEADLK;
        public static readonly int ENAMETOOLONG = Platform.Errno.ENAMETOOLONG;
        public static readonly int ENOLCK = Platform.Errno.ENOLCK;
        public static readonly int ENOSYS = Platform.Errno.ENOSYS;
        public static readonly int ENOTEMPTY = Platform.Errno.ENOTEMPTY;
        public static readonly int EADDRINUSE = Platform.Errno.EADDRINUSE;
        public static readonly int EADDRNOTAVAIL = Platform.Errno.EADDRNOTAVAIL;
        public static readonly int ECONNREFUSED = Platform.Errno.ECONNREFUSED;
        public static readonly int EINPROGRESS = Platform.Errno.EINPROGRESS;
        public static readonly int ENETDOWN = Platform.Errno.ENETDOWN;
        public static readonly int ENOBUFS = Platform.Errno.ENOBUFS;
        public static readonly int ENOTSOCK = Platform.Errno.ENOTSOCK;
        public static readonly int ENOTSUP = Platform.Errno.ENOTSUP;
        public static readonly int EPROTONOSUPPORT = Platform.Errno.EPROTONOSUPPORT;

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:FieldNamesMustNotContainUnderscore",
            Justification = "Mirrors constant defined in zmq.h.")]
        public static readonly int ZMQ_HAUS_NUMERO = 156384712;

        public static readonly int EFSM = ZMQ_HAUS_NUMERO + 51;
        public static readonly int ENOCOMPATPROTO = ZMQ_HAUS_NUMERO + 52;
        public static readonly int ETERM = ZMQ_HAUS_NUMERO + 53;
        public static readonly int EMTHREAD = ZMQ_HAUS_NUMERO + 54;

        public static readonly IDictionary<int, string> ErrorNames;

        static ErrorCode()
        {
            ErrorNames = typeof(ErrorCode)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(int))
                .ToDictionary(f => (int)f.GetValue(null), f => f.Name);
        }

        // ReSharper restore InconsistentNaming
    }
}

#pragma warning restore 1591