using System;

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
        public static short ToInt16(byte[] buffer)
        {
            var i = buffer[0] << 8 |
                    buffer[1];

            return (short)i;
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 16-bit integer derived from the 2 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the short from</param>
        /// <param name="offset">Offset to read from</param>
        /// <returns></returns>
        public static ushort ToUInt16(byte[] buffer, int offset)
        {
            var i = buffer[offset] << 8 |
                    buffer[offset + 1];

            return (ushort)i;
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 16-bit integer derived from the 2 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the short from</param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static ushort ToUInt16(Span<byte> buffer, int offset)
        {
            var i = buffer[offset] << 8 |
                    buffer[offset + 1];

            return (ushort)i;
        }        

        /// <summary>
        /// Given a 16-bit integer, return it as a byte-array in Big-endian order.
        /// </summary>
        /// <param name="value">the short to convert</param>
        /// <returns>a 2-byte array containing that short's bits</returns>
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
        public static void PutInt16(short value, byte[] buffer)
        {
            buffer[0] = (byte)(value >> 8);
            buffer[1] = (byte) value;
        }

        /// <summary>
        /// Given a 16-bit integer, and a byte-array buffer and offset,
        /// - write the 2 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the short to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the short's bytes into</param>
        /// <param name="offset">Offset</param>
        public static void PutUInt16(ushort value, byte[] buffer, int offset = 0)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte) value;
        }
        
        /// <summary>
        /// Given a 16-bit integer, and a byte-array buffer and offset,
        /// - write the 2 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the short to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the short's bytes into</param>
        /// <param name="offset">Offset</param>
        public static void PutUInt16(ushort value, Span<byte> buffer, int offset = 0)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte) value;
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 32-bit integer derived from the 4 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the integer from</param>
        /// <param name="offset">offset</param>
        /// <returns></returns>
        public static int ToInt32(byte[] buffer, int offset = 0)
        {
            return 
                buffer[offset] << 24 |
                buffer[offset + 1] << 16 | 
                buffer[offset + 2] <<  8 | 
                buffer[offset + 3];
        }
        
        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 32-bit integer derived from the 4 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the integer from</param>
        /// <returns></returns>
        public static int ToInt32(Span<byte> buffer)
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
        /// <param name="offset">Offset to write to</param>
        public static void PutInt32(int value, byte[] buffer, int offset = 0)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >>  8);
            buffer[offset + 3] = (byte) value;
        }
        
        /// <summary>
        /// Given a 32-bit integer, and a byte-array buffer and offset,
        /// - write the 4 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the integer to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the integer's bytes into</param>
        public static void PutInt32(int value, Span<byte> buffer)
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
        public static long ToInt64(byte[] buffer)
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
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 64-bit integer derived from the 8 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the Int64 from</param>
        /// <returns></returns>
        public static long ToInt64(Span<byte> buffer)
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
        public static void PutInt64(long value, byte[] buffer)
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

        /// <summary>
        /// Given a 64-bit integer, and a byte-array buffer and offset,
        /// - write the 8 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the long value to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the long value's bytes into</param>
        public static void PutInt64(long value, Span<byte> buffer)
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
        
        /// <summary>
        /// Given a 64-bit integer, and a byte-array buffer and offset,
        /// - write the 8 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the long value to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the long value's bytes into</param>
        public static void PutUInt64(ulong value, Span<byte> buffer)
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

        /// <summary>
        /// Given a 64-bit integer, and a byte-array buffer and offset,
        /// - write the 8 bytes of that integer into the buffer starting at that offset, in Big-endian order.
        /// </summary>
        /// <param name="value">the long value to convert into bytes</param>
        /// <param name="buffer">the byte-array to write the long value's bytes into</param>
        /// <param name="offset">Offset to write to</param>
        public static void PutUInt64(ulong value, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(value >> 56);
            buffer[offset + 1] = (byte)(value >> 48);
            buffer[offset + 2] = (byte)(value >> 40);
            buffer[offset + 3] = (byte)(value >> 32);
            buffer[offset + 4] = (byte)(value >> 24);
            buffer[offset + 5] = (byte)(value >> 16);
            buffer[offset + 6] = (byte)(value >> 8);
            buffer[offset + 7] = (byte) value;
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 64-bit integer derived from the 8 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the Int64 from</param>
        /// <param name="offset">Offset to read from</param>
        /// <returns></returns>
        public static ulong ToUInt64(byte[] buffer, int offset)
        {
            return
                (ulong)buffer[offset] << 56 |
                (ulong)buffer[offset + 1] << 48 |
                (ulong)buffer[offset + 2] << 40 |
                (ulong)buffer[offset + 3] << 32 |
                (ulong)buffer[offset + 4] << 24 |
                (ulong)buffer[offset + 5] << 16 |
                (ulong)buffer[offset + 6] <<  8 |
                (ulong)buffer[offset + 7];
        }

        /// <summary>
        /// Given a byte-array assumed to be in Big-endian order, and an offset into it
        /// - return a 64-bit integer derived from the 8 bytes starting at that offset.
        /// </summary>
        /// <param name="buffer">the byte-array to get the Int64 from</param>
        /// <param name="offset">Offset to read from</param>
        /// <returns></returns>
        public static ulong ToUInt64(Span<byte> buffer, int offset)
        {
            return
                (ulong)buffer[offset] << 56 |
                (ulong)buffer[offset + 1] << 48 |
                (ulong)buffer[offset + 2] << 40 |
                (ulong)buffer[offset + 3] << 32 |
                (ulong)buffer[offset + 4] << 24 |
                (ulong)buffer[offset + 5] << 16 |
                (ulong)buffer[offset + 6] <<  8 |
                (ulong)buffer[offset + 7];
        }
    }
}