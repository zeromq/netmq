using System.Collections.Generic;

namespace NetMQ.Core.Utils
{
    /// <summary>
    /// Class ByteArrayEqualityComparer is an IEqualityComparer for byte-arrays,
    /// and provides an Equals methods to compare two byte-arrays for equality in value
    /// and GetHashCode.
    /// </summary>
    internal sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        private const uint C1 = 0xcc9e2d51;
        private const uint C2 = 0x1b873593;

        /// <summary>
        /// Return true only if the two byte-arrays are equal in length and contain the same values.
        /// </summary>
        /// <param name="x">one of the two byte-arrays to compare</param>
        /// <param name="y">the other byte-array to compare against</param>
        /// <returns></returns>
        public bool Equals(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return a numeric hashcode of the given byte-array.
        /// </summary>
        /// <param name="data">the given byte-array to compute the hashcode of</param>
        /// <returns>an integer that contains a hashcode computed over the byte-array</returns>
        public int GetHashCode(byte[] data)
        {
            unchecked
            {
                int remainder = data.Length & 3;
                int alignedLength = data.Length - remainder;

                uint hash = 0;

                // Walk through data four bytes at a time
                for (int i = 0; i < alignedLength; i += 4)
                {
                    var k = (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24);
                    k *= C1;
                    k = (k << 15) | (k >> (32 - 15));
                    k *= C2;

                    hash ^= k;
                    hash = (hash << 13) | (hash >> (32 - 13));
                    hash = hash * 5 + 0xe6546b64;
                }

                // Deal with the one, two or three leftover bytes
                if (remainder > 0)
                {
                    uint k = 0;

                    // determine how many bytes we have left to work with based on length
                    switch (remainder)
                    {
                        case 3:
                            k ^= (uint)data[alignedLength + 2] << 16;
                            goto case 2;
                        case 2:
                            k ^= (uint)data[alignedLength + 1] << 8;
                            goto case 1;
                        case 1:
                            k ^= data[alignedLength];
                            break;
                    }

                    k *= C1;
                    k = (k << 15) | (k >> (32 - 15));
                    k *= C2;

                    hash ^= k;
                }

                hash ^= (uint)data.Length;
                hash ^= hash >> 16;
                hash *= 0x85ebca6b;
                hash ^= hash >> 13;
                hash *= 0xc2b2ae35;
                hash ^= hash >> 16;

                return (int)hash;
            }
        }
    }
}
