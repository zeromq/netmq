using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Utils
{
    static class ArrayExtensions
    {
        public static T[] Resize<T>(this T[] array, int size, bool reverse)
        {
            T[] dest;

            if (size > array.Length)
            {
                dest = new T[size];
                if (reverse)
                    Array.Copy(array, 0, dest, 0, array.Length);
                else
                    Array.Copy(array, 0, dest, size - array.Length, array.Length);
            }
            else if (size < array.Length)
            {
                dest = new T[size];
                if (reverse)
                    Array.Copy(array, 0, dest, 0, size);
                else
                    Array.Copy(array, array.Length - size, dest, 0, size);

            }
            else
            {
                dest = array;
            }

            return dest;
        }

        public static void Swap<T>(this IList<T> array, int index1, int index2)
        {
            if (index1 == index2)
                return;

            T item1 = array[index1];
            T item2 = array[index2];
            if (item1 != null)
                array[index2] = item1;
            if (item2 != null)
                array[index1] = item2;
        }
    }
}
