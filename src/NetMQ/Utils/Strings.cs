using System.Diagnostics.CodeAnalysis;

namespace NetMQ
{
    internal static class Strings
    {
        /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
        public static bool IsNullOrEmpty([NotNullWhen(returnValue: false)] string? s) => string.IsNullOrEmpty(s);

        /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
        public static bool IsNullOrWhiteSpace([NotNullWhen(returnValue: false)] string? s) => string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Gets a hash value from a string. This hash is stable across process invocations (doesn't use hash randomization) and across different .NET versions and platforms.
        /// </summary>
        /// <remarks>
        /// The original code was taken from <c>string.GetHashCode()</c> with some minor changes
        /// https://github.com/microsoft/referencesource/blob/master/mscorlib/system/string.cs
        /// </remarks>
        public static int GetStableHashCode(string str)
        {
            int hash1 = 5381;
            int hash2 = hash1;

            int i = 0;

            while (i < str.Length)
            {
                char c = str[i];

                hash1 = ((hash1 << 5) + hash1) ^ c;

                i++;

                if (i == str.Length)
                {
                    break;
                }

                c = str[i];

                hash2 = ((hash2 << 5) + hash2) ^ c;

                i++;
            }

            return hash1 + (hash2 * 1566083941);
        }
    }
}
