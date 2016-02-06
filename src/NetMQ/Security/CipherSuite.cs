using System;

namespace NetMQ.Security
{
    /// <summary>
    /// This (byte flag) enum-type represents known cipher suites
    /// that are available with SecureChannel.
    /// </summary>
    /// <remarks>
    /// TLS stands for Transport Layer Security and is the successor to Secure Sockets Layer (SSL).
    /// These are cryptographic protocols designed to provide communications security over a network.
    /// See https://www.thesprawl.org/research/tls-and-ssl-cipher-suites/ for details regarding the details of what these represent.
    /// RSA stands for Rivest, Shamir, Adleman.
    /// SHA stands for Secure Hash Algorithm.
    /// </remarks>
    [Flags]
    public enum CipherSuite : byte
    {
        //  Protocol   Kx   Au   Enc     Bits   Mac

        /// <summary>
        /// The Null TLS cipher suite. This does not provide any data encryption nor data integrity function
        /// and is used during initial session establishment.
        /// </summary>
        TLS_NULL_WITH_NULL_NULL = 0,

        /// <summary>
        /// Cipher ID 2. TLS cipher with the RSA key-exchange algorithm, and SHA hashing algorithm.
        /// </summary>
        TLS_RSA_WITH_NULL_SHA = 0x02,

        /// <summary>
        /// Cipher ID 0x3B. TLS cipher with the RSA key-exchange algorithm, and SHA256 hashing algorithm.
        /// </summary>
        TLS_RSA_WITH_NULL_SHA256 = 0x3B,

        /// <summary>
        /// Cipher ID 0x2F. TLS protocol with the RSA key-exchange and authentication algorithms,
        /// the AES_128_CBC (128-bit) symmetric encryption algorithm, and SHA hashing algorithm.
        /// </summary>
        TLS_RSA_WITH_AES_128_CBC_SHA = 0x2F,

        /// <summary>
        /// Cipher ID 0x35. TLS protocol with the RSA key-exchange and authentication algorithms,
        /// the AES_256_CBC (256-bit) symmetric encryption algorithm, and SHA hashing algorithm.
        /// </summary>
        TLS_RSA_WITH_AES_256_CBC_SHA = 0x35,

        /// <summary>
        /// Cipher ID 0x3C. TLS protocol with the RSA key-exchange and authentication algorithms,
        /// the AES_128_CBC (128-bit) symmetric encryption algorithm, and SHA256 hashing algorithm.
        /// </summary>
        TLS_RSA_WITH_AES_128_CBC_SHA256 = 0x3C,

        /// <summary>
        /// Cipher ID 0x3D. TLS protocol with the RSA key-exchange and authentication algorithms,
        /// the AES_256_CBC (256-bit) symmetric encryption algorithm, and SHA256 hashing algorithm.
        /// </summary>
        TLS_RSA_WITH_AES_256_CBC_SHA256 = 0x3D,
    }
}
