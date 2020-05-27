using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NetMQ
{
    internal static class Assumes
    {
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
        [Conditional("DEBUG")]
        public static void NotNull<T>([NotNull] T o) where T : class?
        {
            Debug.Assert(o is object, $"Unexpected null of type {typeof(T).Name}");
        }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.
    }
}
