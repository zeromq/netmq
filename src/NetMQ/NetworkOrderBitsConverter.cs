using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// This static class serves to convert between byte-arrays, and various integer sizes
    /// - all of which assume the byte-data is in Big-endian, or "Network Byte Order".
    /// </summary>
    public static class NetworkOrderBitsConverter
    {
        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 16-bit integer derived from the 2 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the short from</param>
        /// <returns></returns>
        public static short ToInt16([NotNull] byte[] buffer)
        {
            var i = buffer[0] << 8 |
                    buffer[1];

            return (short)i;
        }

        /// <summary>
        /// Given a 16-bit integer, return it as a byte-array in Big-endian order.
        /// </summary>
        /// <param name="value">the short to convert</param>
        /// <returns>a 2-byte array containing that short's bits</returns>
        [NotNull]
        public static byte[] GetBytes(short value)
        {
            var buffer = new byte[2];
            PutInt16(value, buffer);

            return buffer;
        }

        /// <summary>
        /// Given a 16-bit integer, and a byte-array buffer and offset,
        /// - write the 2 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the short to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the short's bytes into</param>
        public static void PutInt16(short value, [NotNull] byte[] buffer)
        {
            buffer[0] = (byte)(value >> 8);
            buffer[1] = (byte) value;
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 32-bit integer derived from the 4 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the integer from</param>
        /// <returns></returns>
        public static int ToInt32([NotNull] byte[] buffer)
        {
            return 
                buffer[0] << 24 |
                buffer[1] << 16 | 
                buffer[2] <<  8 | 
                buffer[3];
        }

        /// <summary>
        /// Given a 32-bit integer, return it as a byte-array in Big-endian order.
        /// </summary>
        /// <param name="value">the int to convert</param>
        /// <returns>a 4-byte array containing that integer's bits</returns>
        [NotNull]
        public static byte[] GetBytes(int value)
        {
            var buffer = new byte[4];
            PutInt32(value, buffer);

            return buffer;
        }

        /// <summary>
        /// Given a 32-bit integer, and a byte-array buffer and offset,
        /// - write the 4 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the integer to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the integer's bytes into</param>
        public static void PutInt32(int value, [NotNull] byte[] buffer)
        {
            buffer[0] = (byte)(value >> 24);
            buffer[1] = (byte)(value >> 16);
            buffer[2] = (byte)(value >>  8);
            buffer[3] = (byte) value;
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 64-bit integer derived from the 8 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the Int64 from</param>
        /// <returns></returns>
        public static long ToInt64([NotNull] byte[] buffer)
        {
            return
                (long)buffer[0] << 56 |
                (long)buffer[1] << 48 |
                (long)buffer[2] << 40 |
                (long)buffer[3] << 32 |
                (long)buffer[4] << 24 |
                (long)buffer[5] << 16 |
                (long)buffer[6] <<  8 |
                (long)buffer[7];
        }

        /// <summary>
        /// Given a 64-bit integer, return it as a byte-array in Big-endian order.
        /// </summary>
        /// <param name="value">The <c>long</c> value to convert from.</param>
        /// <returns>The network order presentation of <paramref name="value"/> as an 8-byte array.</returns>
        [NotNull]
        public static byte[] GetBytes(long value)
        {
            var buffer = new byte[8];
            PutInt64(value, buffer);

            return buffer;
        }

        /// <summary>
        /// Given a 64-bit integer, and a byte-array buffer and offset,
        /// - write the 8 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the long value to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the long value's bytes into</param>
        public static void PutInt64(long value, [NotNull] byte[] buffer)
        {
            buffer[0] = (byte)(value >> 56);
            buffer[1] = (byte)(value >> 48);
            buffer[2] = (byte)(value >> 40);
            buffer[3] = (byte)(value >> 32);
            buffer[4] = (byte)(value >> 24);
            buffer[5] = (byte)(value >> 16);
            buffer[6] = (byte)(value >> 8);
            buffer[7] = (byte) value;
        }
    }
}