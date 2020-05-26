namespace NetMQ
{
    /// <summary>
    /// This enum-type specifies either big-endian (Big) or little-endian (Little),
    /// which indicate whether the most-significant bits are placed first or last in memory.
    /// </summary>
    public enum Endianness
    {
        /// <summary>
        /// Most-significant bits are placed first in memory.
        /// </summary>
        Big,

        /// <summary>
        /// Most-significant bits are placed last in memory.
        /// </summary>
        Little
    }
}