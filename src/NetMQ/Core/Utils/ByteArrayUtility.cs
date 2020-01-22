namespace NetMQ.Core.Utils
{
    internal static class ByteArrayUtility
    {
        public static bool AreEqual(byte[] a, int aOffset, byte[] b, int bOffset, int count)
        {
            if (aOffset + count > a.Length)
                return false;

            if (bOffset + count > b.Length)
                return false;
            
            for (int i = 0; i < count; i++)
            {
                if (a[aOffset + i] != b[bOffset + i])
                    return false;
            }

            return true;
        }
    }
}