namespace ZeroMQ.Interop
{
    using System;
    using System.IO;
    using System.Reflection;

    internal static class ManifestResource
    {
        public static bool Extract(string resourceName, string outputPath)
        {
            if (File.Exists(outputPath))
            {
                // This is necessary to prevent access conflicts if multiple processes are run from the
                // same location. The naming scheme implemented in UnmanagedLibrary should ensure that
                // the correct version is always used.
                return true;
            }

            Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (resourceStream == null)
            {
                // No manifest resources were compiled into the current assembly. This is likely a 'manual
                // deployment' situation, so do not throw an exception at this point and allow all deployment
                // paths to be searched.
                return false;
            }

            try
            {
                using (FileStream fileStream = File.Create(outputPath))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Caller does not have write permission for the current file
                return false;
            }

            return true;
        }
    }
}
