using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NetMQ.Core.Patterns.Utils
{
    internal static class ArrayExtensions
    {
        /// <summary>
        /// Make resize operation on array.
        /// </summary>
        /// <typeparam name="T">Type of containing data.</typeparam>
        /// <param name="src">Source array.</param>
        /// <param name="size">New size of array.</param>
        /// <param name="ended">If grow/shrink operation should be applied to the end of array.</param>
        /// <returns>Resized array.</returns>
        [NotNull]
        public static T[] Resize<T>([NotNull] this T[] src, int size, bool ended)
        {
            T[] dest;

            if (size > src.Length)
            {
                dest = new T[size];
                if (ended)
                    Array.Copy(src, 0, dest, 0, src.Length);
                else
                    Array.Copy(src, 0, dest, size - src.Length, src.Length);
            }
            else if (size < src.Length)
            {
                dest = new T[size];
                if (ended)
                    Array.Copy(src, 0, dest, 0, size);
                else
                    Array.Copy(src, src.Length - size, dest, 0, size);
            }
            else
            {
                dest = src;
            }

            return dest;
        }

        public static void Swap<T>([NotNull] this List<T> items, int index1, int index2) where T : class
        {
            if (index1 == index2) 
                return;

            T item1 = items[index1];
            T item2 = items[index2];
            if (item1 != null)
                items[index2] = item1;
            if (item2 != null)
                items[index1] = item2;
        }
    }
}
