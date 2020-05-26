using System.Diagnostics.CodeAnalysis;

namespace NetMQ
{
    internal static class Strings
    {
        /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
        public static bool IsNullOrEmpty([NotNullWhen(returnValue: false)] string? s) => string.IsNullOrEmpty(s);

        /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
        public static bool IsNullOrWhiteSpace([NotNullWhen(returnValue: false)] string? s) => string.IsNullOrWhiteSpace(s);
    }
}
