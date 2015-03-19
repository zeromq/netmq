namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// This enum-type specifies which end of the connection -- Server or Client.
    /// </summary>
    public enum ConnectionEnd
    {
        /// <summary>
        /// This is the server-end of the client/server connection.
        /// </summary>
        Server,

        /// <summary>
        /// This is the client-end of the client/server connection.
        /// </summary>
        Client
    }

    /// <summary>
    /// This enum-type specifies the Pseudo-Random-Function (PRF), which is one of the cryptographic algorithms
    /// negotiated in the Internet Key Exchange (IKE) protocol (see RFC 4306).
    /// It currently has only one value - SHA256.
    /// </summary>
    public enum PRFAlgorithm
    {
        /// <summary>
        /// The PRF used the Secure Hash Algorithm (SHA) known as SHA-256, which is of the SHA-2 family.
        /// SHA-256 uses 32-bit words.
        /// </summary>
        SHA256
    }

    /// <summary>
    /// This enum-type specifies the bulk-cipher algorithm - presently the only values are Null and AES.
    /// </summary>
    public enum BulkCipherAlgorithm
    {
        /// <summary>
        /// Null - no cipher algorithm specified (the default value of this enum-type).
        /// </summary>
        Null = 0,

        /// <summary>
        /// The Advanced Encryption Standard (AES) symmetric-key algorithm.
        /// </summary>
        AES = 3
    }

    /// <summary>
    /// This enum-type specifies the type of cipher - presently the only value is Block.
    /// </summary>
    public enum CipherType
    {
        /// <summary>
        /// A block cipher, in which a cryptographic key and algorithm are applied to a block of data.
        /// This is the only value provided currently.
        /// </summary>
        Block = 1
    }

    /// <summary>
    /// This enum-type specifies the algorithm used to generate the Message Authentication Code (MAC).
    /// </summary>
    public enum MACAlgorithm
    {
        /// <summary>
        /// No MAC-algorithm is specified.
        /// </summary>
        Null = 0,

        /// <summary>
        /// This indicates the Hashed Message Authentication Code (HMAC) is generated using SHA-1.
        /// </summary>
        HMACSha1 = 2,

        /// <summary>
        /// Generate the Hashed Message Authentication Code (HMAC) using SHA-256,
        /// a Secure Hash Algorithm (SHA) that is a successor to SHA-1.
        /// </summary>
        HMACSha256
    }

    /// <summary>
    /// This enum-type specifies the method of compression - presently the only value is Null.
    /// </summary>
    public enum CompressionMethod
    {
        /// <summary>
        /// No compression-method is specified.
        /// </summary>
        Null = 0,
    }

    /// <summary>
    /// Class SecurityParameters holds information to completely specify the parameters for a specific secure message-exchange session.
    /// </summary>
    public class SecurityParameters
    {
        /// <summary>
        /// Get or set which end of the connection - Server or Client.
        /// </summary>
        public ConnectionEnd Entity { get; set; }

        /// <summary>
        /// Get or set the Pseudo-Random-Function used to compute the random number - which here is SHA256.
        /// </summary>
        public PRFAlgorithm PRFAlgorithm { get; set; }

        /// <summary>
        /// Get or set the bulk-cipher algorithm, which is either Null or AES.
        /// </summary>
        public BulkCipherAlgorithm BulkCipherAlgorithm { get; set; }

        /// <summary>
        /// Get or set the type of cipher ( <see cref="CipherType"/> ) - which is Block.
        /// </summary>
        public CipherType CipherType { get; set; }

        /// <summary>
        /// Get or set the length of the encryption-key, as a byte.
        /// Here this is either 0, 16, or 32.
        /// </summary>
        public byte EncKeyLength { get; set; }

        /// <summary>
        /// Get or set the length of the cipher-block used in encryption/decryption.
        /// </summary>
        public byte BlockLength { get; set; }

        /// <summary>
        /// Get or set the length in bytes of the fixed Initialization-Vector.
        /// </summary>
        public byte FixedIVLength { get; set; }

        /// <summary>
        /// Get or set the length of the initialization-vector record.
        /// </summary>
        public byte RecordIVLength { get; set; }

        /// <summary>
        /// Get or set the MAC-algorithm.
        /// </summary>
        public MACAlgorithm MACAlgorithm { get; set; }

        /// <summary>
        /// Get or set the
        /// </summary>
        public byte MACLength { get; set; }

        /// <summary>
        /// Get or set the length of the MAC key, as a byte.
        /// </summary>
        public byte MACKeyLength { get; set; }

        /// <summary>
        /// Get or set the compression algorithm to use.
        /// </summary>
        public CompressionMethod CompressionAlgorithm { get; set; }

        /// <summary>
        /// Get or set the byte-array that represents the master-secret part of the SSL/TLS protocol.
        /// </summary>
        public byte[] MasterSecret { get; set; }

        /// <summary>
        /// Get or set the byte-array the represents the client random-number.
        /// </summary>
        public byte[] ClientRandom { get; set; }

        /// <summary>
        /// Get or set the byte-array the represents the server random-number.
        /// </summary>
        public byte[] ServerRandom { get; set; }
    }
}
