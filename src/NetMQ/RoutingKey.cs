using System;

namespace NetMQ
{
    /// <summary>
    /// Structure to represent a routing key from Router, Peer and Stream sockets.
    /// Implement Equals and GetHashCode and can be used as key for a Dictionary.
    /// </summary>
    public struct RoutingKey: IEquatable<RoutingKey>, IEquatable<byte[]>
    {
        private byte[] bytes;

        private const uint C1 = 0xcc9e2d51;
        private const uint C2 = 0x1b873593;

        /// <summary>
        /// Create a new routing key out of a byte array
        /// </summary>
        /// <param name="array"></param>
        public RoutingKey(byte[] array)
        {
            bytes = array;
        }

        /// <summary>
        /// Create a new routing key out of a base64 string
        /// </summary>
        /// <param name="b64"></param>
        public RoutingKey(string b64)
        {
            bytes = Convert.FromBase64String(b64);
        }

        internal byte[] Bytes
        {
            get { return bytes; }
        }

        /// <summary>
        /// Check if routing-key is equal to the object
        /// </summary>
        /// <param name="obj">Object to compare against, valid types are byte-array and RoutingKey</param>
        /// <returns>True if equals, otherwise false</returns>
        public override bool Equals(object obj)
        {
            if (obj is RoutingKey)
                return Equals((RoutingKey) obj);

            if (obj is byte[])
                return Equals((byte[])obj);

            return false;
        }

        /// <summary>
        /// Check if routing-key is equal to the byte-array
        /// </summary>
        /// <param name="x">Byte-array to compare against</param>
        /// <returns>True if equals, otherwise false</returns>
        public bool Equals(byte[] x)
        {
            if (bytes.Length != x.Length)
            {
                return false;
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != x[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if routing-key is equal to another routing-key
        /// </summary>
        /// <param name="x">Other routing key to check against</param>
        /// <returns>True if equals, otherwise false</returns>
        public bool Equals(RoutingKey x)
        {
            if (bytes.Length != x.bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != x.bytes[i])
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
        public override int GetHashCode()
        {
            unchecked
            {
                int remainder = bytes.Length & 3;
                int alignedLength = bytes.Length - remainder;

                uint hash = 0;

                // Walk through data four bytes at a time
                for (int i = 0; i < alignedLength; i += 4)
                {
                    var k = (uint)(bytes[i] | bytes[i + 1] << 8 | bytes[i + 2] << 16 | bytes[i + 3] << 24);
                    k *= C1;
                    k = (k << 15) | (k >> (32 - 15));
                    k *= C2;

                    hash ^= k;
                    hash = (hash << 13) | (hash >> (32 - 13));
                    hash = (hash * 5) + 0xe6546b64;
                }

                // Deal with the one, two or three leftover bytes
                if (remainder > 0)
                {
                    uint k = 0;

                    // determine how many bytes we have left to work with based on length
                    switch (remainder)
                    {
                        case 3:
                            k ^= (uint)bytes[alignedLength + 2] << 16;
                            goto case 2;
                        case 2:
                            k ^= (uint)bytes[alignedLength + 1] << 8;
                            goto case 1;
                        case 1:
                            k ^= bytes[alignedLength];
                            break;
                    }

                    k *= C1;
                    k = (k << 15) | (k >> (32 - 15));
                    k *= C2;

                    hash ^= k;
                }

                hash ^= (uint)bytes.Length;
                hash ^= hash >> 16;
                hash *= 0x85ebca6b;
                hash ^= hash >> 13;
                hash *= 0xc2b2ae35;
                hash ^= hash >> 16;

                return (int)hash;
            }
        }

        /// <summary>
        /// Convert the routing-key to base64 string
        /// </summary>
        /// <returns>Base64 string representation of the routing-key</returns>
        public override string ToString()
        {
            return Convert.ToBase64String(bytes);
        }
    }
}