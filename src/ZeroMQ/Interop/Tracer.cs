namespace ZeroMQ.Interop
{
    using System.Diagnostics;

    internal class Tracer
    {
        private static readonly TraceSwitch AssemblySwitch = new TraceSwitch("clrzmq", "ZeroMQ C# Binding");

        public static void Verbose(string message, string category)
        {
            if (AssemblySwitch.TraceVerbose)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void Info(string message, string category)
        {
            if (AssemblySwitch.TraceInfo)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void Warning(string message, string category)
        {
            if (AssemblySwitch.TraceWarning)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void Error(string message, string category)
        {
            if (AssemblySwitch.TraceError)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void VerboseIf(bool condition, string message, string category)
        {
            if (AssemblySwitch.TraceVerbose && condition)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void InfoIf(bool condition, string message, string category)
        {
            if (AssemblySwitch.TraceInfo && condition)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void WarningIf(bool condition, string message, string category)
        {
            if (AssemblySwitch.TraceWarning && condition)
            {
                Trace.WriteLine(message, category);
            }
        }

        public static void ErrorIf(bool condition, string message, string category)
        {
            if (AssemblySwitch.TraceError && condition)
            {
                Trace.WriteLine(message, category);
            }
        }
    }
}
