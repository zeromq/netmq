namespace ZeroMQ.Interop
{
    using System.Security.Permissions;

    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Safe handle for unmanaged libraries. See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ for more about safe handles.
    /// </summary>
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
    internal sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeLibraryHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return Platform.ReleaseHandle(handle);
        }
    }
}
