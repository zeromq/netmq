using System;

namespace NetMQ.Utils
{
    internal static class ArrayExtensions
    {
        public static Span<T> Slice<T>(this T[] array, int offset)
        {
            Span<T> span = array;
            return span.Slice(offset);
        }
        
        public static Span<T> Slice<T>(this T[] array, int offset, int length)
        {
            Span<T> span = array;
            return span.Slice(offset, length);
        }
    }
}