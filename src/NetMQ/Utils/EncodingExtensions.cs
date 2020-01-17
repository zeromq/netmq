using System;
using System.Text;

namespace NetMQ.Utils
{
    internal static class EncodingExtensions
    {
#if !NETSTANDARD2_1        
        public static unsafe void GetBytes(this Encoding encoding, string str, Span<byte> bytes)
        {
            fixed (char* s = str)
                fixed (byte* p = bytes)
                    encoding.GetBytes(s, str.Length, p, bytes.Length);
            
        }
#endif        
    }
}