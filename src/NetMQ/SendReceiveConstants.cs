using System;
using System.Text;

namespace NetMQ
{
    /// <summary>
    /// Constants for the send and receive operation
    /// </summary>
    public static class SendReceiveConstants
    {
        /// <summary>
        /// The <see cref="Encoding"/> used in string related methods that do
        /// not explicitly provide an encoding parameter.
        /// </summary>
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;

        /// <summary>Indicates an infinite timeout for send and receive operations.</summary>
        public static readonly TimeSpan InfiniteTimeout = TimeSpan.FromMilliseconds(-1);
    }
}

