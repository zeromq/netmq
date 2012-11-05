namespace ZeroMQ.Interop
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Utility class to wrap an unmanaged shared lib and be responsible for freeing it.
    /// </summary>
    /// <remarks>
    /// This is a managed wrapper over the native LoadLibrary, GetProcAddress, and FreeLibrary calls on Windows
    /// and dlopen, dlsym, and dlclose on Posix environments.
    /// </remarks>
    internal sealed class UnmanagedLibrary : IDisposable
    {
        private const string TraceCategory = "clrzmq[UnmanagedLibrary]";

        private static readonly string CurrentArch = Environment.Is64BitProcess ? "x64" : "x86";

        private readonly string _systemFileName;
        private readonly string _extractedFileName;
        private readonly SafeLibraryHandle _handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnmanagedLibrary"/> class. Loads a dll and takes responible for freeing it.
        /// </summary>
        /// <remarks>Throws exceptions on failure. Most common failure would be file-not-found, that the file is not a loadable image.</remarks>
        /// <param name="fileName">full path name of dll to load</param>
        /// <exception cref="System.IO.FileNotFoundException">if fileName can't be found</exception>
        public UnmanagedLibrary(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException("fileName");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A valid file name is expected.", "fileName");
            }

            _systemFileName = fileName + Platform.LibSuffix;
            _handle = LoadFromSystemPath();

            Tracer.InfoIf(_handle != null, "Loading " + _systemFileName + " from LoadLibrary system path.", TraceCategory);

            if (_handle == null)
            {
                // Ensure the correct manifest resource will always be used, even if it has already been extracted
                _extractedFileName = fileName + "-" + CurrentArch + "-" + Assembly.GetExecutingAssembly().GetName().Version + Platform.LibSuffix;

                _handle = LoadFromAssemblyPath() ?? LoadFromExecutingPath() ?? LoadFromTempPath();
            }

            if (_handle == null || _handle.IsInvalid)
            {
                throw new FileNotFoundException(
                    "Unable to find " + _systemFileName + " on system path or extract it from assembly manifest resources. Inspect Trace output for more details.",
                    _systemFileName,
                    Platform.GetLastLibraryError());
            }
        }

        /// <summary>
        /// Dynamically look up a function in the dll via kernel32!GetProcAddress or libdl!dlsym.
        /// </summary>
        /// <typeparam name="TDelegate">Delegate type to load</typeparam>
        /// <param name="functionName">Raw name of the function in the export table.</param>
        /// <returns>A delegate to the unmanaged function.</returns>
        /// <exception cref="MissingMethodException">Thrown if the given function name is not found in the library.</exception>
        /// <remarks>
        /// GetProcAddress results are valid as long as the dll is not yet unloaded. This
        /// is very very dangerous to use since you need to ensure that the dll is not unloaded
        /// until after you're done with any objects implemented by the dll. For example, if you
        /// get a delegate that then gets an IUnknown implemented by this dll,
        /// you can not dispose this library until that IUnknown is collected. Else, you may free
        /// the library and then the CLR may call release on that IUnknown and it will crash.
        /// </remarks>
        public TDelegate GetUnmanagedFunction<TDelegate>(string functionName) where TDelegate : class
        {
            IntPtr p = Platform.LoadProcedure(_handle, functionName);

            if (p == IntPtr.Zero)
            {
                throw new MissingMethodException("Unable to find function '" + functionName + "' in dynamically loaded library.");
            }

            // Ideally, we'd just make the constraint on TDelegate be
            // System.Delegate, but compiler error CS0702 (constrained can't be System.Delegate)
            // prevents that. So we make the constraint system.object and do the cast from object-->TDelegate.
            return (TDelegate)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(TDelegate));
        }

        public void Dispose()
        {
            if (!_handle.IsClosed)
            {
                _handle.Close();
            }
        }

        private static SafeLibraryHandle NullifyInvalidHandle(SafeLibraryHandle handle)
        {
            return handle.IsInvalid ? null : handle;
        }

        private static string GetFullAssemblyPath()
        {
            var dir = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            var fi = new FileInfo(Uri.UnescapeDataString(dir.AbsolutePath));
            return fi.Directory != null ? fi.Directory.FullName : null;
        }

        private SafeLibraryHandle LoadFromSystemPath()
        {
            return NullifyInvalidHandle(Platform.OpenHandle(_systemFileName));
        }

        private SafeLibraryHandle LoadFromAssemblyPath()
        {
            var assemblyPath = GetFullAssemblyPath();

            Tracer.WarningIf(assemblyPath == null, "Unable to determine full assembly path.", TraceCategory);

            return assemblyPath != null ? ExtractAndLoadFromPath(assemblyPath) : null;
        }

        private SafeLibraryHandle LoadFromExecutingPath()
        {
            return ExtractAndLoadFromPath(".");
        }

        private SafeLibraryHandle LoadFromTempPath()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            string dir = Path.Combine(Path.GetTempPath(), assemblyName.Name + "-" + assemblyName.Version);
            Directory.CreateDirectory(dir);

            return ExtractAndLoadFromPath(dir);
        }

        private SafeLibraryHandle ExtractAndLoadFromPath(string dir)
        {
            string libPath = Path.GetFullPath(Path.Combine(dir, _extractedFileName));
            string platformSuffix = "." + CurrentArch;

            if (!ManifestResource.Extract(_systemFileName + platformSuffix, libPath))
            {
                Tracer.Warning("Unable to extract native library to " + libPath, TraceCategory);
                return null;
            }

            Tracer.Info("Extracted and loading " + libPath, TraceCategory);
            return NullifyInvalidHandle(Platform.OpenHandle(libPath));
        }
    }
}
