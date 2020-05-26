using System;
using System.Text;

namespace NetMQ.Core.Utils
{
    internal static class SpanUtility
    {
        public static string ToAscii(Span<byte> bytes)
        {
#if NETSTANDARD2_1
            return Encoding.ASCII.GetString(bytes);
#else
            return Encoding.ASCII.GetString(bytes.ToArray());
#endif
        }
        
        public static bool Equals(Span<byte> a, Span<byte> b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }
    }
}