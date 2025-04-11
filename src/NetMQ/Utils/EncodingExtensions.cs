#if !NETSTANDARD2_1 && !NET8_0_OR_GREATER
using System;
using System.Text;

namespace NetMQ.Utils
{
    internal static class EncodingExtensions
    {
        public static unsafe void GetBytes(this Encoding encoding, string? str, Span<byte> bytes)
        {
            if (Strings.IsNullOrEmpty(str))
                return;
            
            fixed (char* s = str)
                fixed (byte* p = bytes)
                    encoding.GetBytes(s, str.Length, p, bytes.Length);
        }
    }
}
#endif        
