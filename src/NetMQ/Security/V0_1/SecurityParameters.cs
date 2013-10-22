
namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// This enum-type specifies which end of the connection -- Server or Client.
    /// </summary>
    public enum ConnectionEnd
    {
        Server,
        Client
    }

    /// <summary>
    /// This enum-type specifies the Pseudo-Random-Function (PRF), which is one of the cryptographic algorithms
    /// negotiated in the Internet Key Exchange (IKE) protocol (see RFC 4306).
    /// It currently has only one value - SHA256.
    /// </summary>
    public enum PRFAlgorithm
    {
        SHA256
    }

    /// <summary>
    /// This enum-type specifies the bulk-cipher algorithm - presently the only values are Null and AES.
    /// </summary>
    public enum BulkCipherAlgorithm
    {
        Null = 0,
        AES = 3
    }

    /// <summary>
    /// This enum-type specifies the type of cipher - presently the only value is Block.
    /// </summary>
    public enum CipherType
    {
        Block = 1
    }

    /// <summary>
    /// This enum-type specifies the algorithm used to generate the Message Authentication Code (MAC).
    /// </summary>
    public enum MACAlgorithm
    {
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
        Null = 0,
    }


    public class SecurityParameters
    {
        public ConnectionEnd Entity { get; set; }
        public PRFAlgorithm PRFAlgorithm { get; set; }
        public BulkCipherAlgorithm BulkCipherAlgorithm { get; set; }
        public CipherType CipherType { get; set; }
        public byte EncKeyLength { get; set; }
        public byte BlockLength { get; set; }
        /// <summary>
        /// The length in bytes of the fixed Initialization-Vector.
        /// </summary>
        public byte FixedIVLength { get; set; }
        public byte RecordIVLength { get; set; }
        public MACAlgorithm MACAlgorithm { get; set; }
        public byte MACLength { get; set; }
        public byte MACKeyLength { get; set; }
        public CompressionMethod CompressionAlgorithm { get; set; }
        public byte[] MasterSecret { get; set; }
        public byte[] ClientRandom { get; set; }
        public byte[] ServerRandom { get; set; }
    }
}
