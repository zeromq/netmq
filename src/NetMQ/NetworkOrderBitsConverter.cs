using JetBrains.Annotations;

namespace NetMQ
{
    public static class NetworkOrderBitsConverter
    {
        public static int ToInt32([NotNull] byte[] buffer, int offset = 0)
        {
            return ((buffer[offset]) << 24) | ((buffer[offset + 1]) << 16) | ((buffer[offset + 2]) << 8) | (buffer[offset + 3]);
        }

        [NotNull]
        public static byte[] GetBytes(int value)
        {
            var buffer = new byte[4];
            PutInt32(value, buffer, 0);

            return buffer;
        }

        public static void PutInt32(int value, [NotNull] byte[] buffer, int offset)
        {
            buffer[offset    ] = (byte)(((value) >> 24) & 0xff);
            buffer[offset + 1] = (byte)(((value) >> 16) & 0xff);
            buffer[offset + 2] = (byte)(((value) >> 8 ) & 0xff);
            buffer[offset + 3] = (byte)(value & 0xff);
        }

        public static long ToInt64([NotNull] byte[] buffer, int offset = 0)
        {
            return
                (((long)buffer[offset])     << 56) |
                (((long)buffer[offset + 1]) << 48) |
                (((long)buffer[offset + 2]) << 40) |
                (((long)buffer[offset + 3]) << 32) |
                (((long)buffer[offset + 4]) << 24) |
                (((long)buffer[offset + 5]) << 16) |
                (((long)buffer[offset + 6]) << 8) |
                 ((long)buffer[offset + 7]);
        }

        [NotNull]
        public static byte[] GetBytes(long value)
        {
            var buffer = new byte[8];
            PutInt64(value, buffer, 0);

            return buffer;
        }

        public static void PutInt64(long value, [NotNull] byte[] buffer, int offset)
        {
            buffer[offset    ] = (byte)(((value) >> 56) & 0xff);
            buffer[offset + 1] = (byte)(((value) >> 48) & 0xff);
            buffer[offset + 2] = (byte)(((value) >> 40) & 0xff);
            buffer[offset + 3] = (byte)(((value) >> 32) & 0xff);
            buffer[offset + 4] = (byte)(((value) >> 24) & 0xff);
            buffer[offset + 5] = (byte)(((value) >> 16) & 0xff);
            buffer[offset + 6] = (byte)(((value) >> 8 ) & 0xff);
            buffer[offset + 7] = (byte)(value & 0xff);
        }
    }
}