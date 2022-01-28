using System;
using System.Collections.Generic;
using System.Linq;
using NaCl;

namespace NetMQ
{
    /// <summary>
    /// Certificate to be used for the Curve encryption
    /// </summary>
    public class NetMQCertificate
    {
        //  Z85 codec, taken from 0MQ RFC project, implements RFC32 Z85 encoding

        //  Maps base 256 to base 85
        private static string Encoder = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

        //  Maps base 85 to base 256
        //  We chop off lower 32 and higher 128 ranges
        //  0xFF denotes invalid characters within this range
        private static byte[] Decoder = {
          0xFF, 0x44, 0xFF, 0x54, 0x53, 0x52, 0x48, 0xFF, 0x4B, 0x4C, 0x46, 0x41,
          0xFF, 0x3F, 0x3E, 0x45, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
          0x08, 0x09, 0x40, 0xFF, 0x49, 0x42, 0x4A, 0x47, 0x51, 0x24, 0x25, 0x26,
          0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32,
          0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x4D,
          0xFF, 0x4E, 0x43, 0xFF, 0xFF, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
          0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C,
          0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x4F, 0xFF, 0x50, 0xFF, 0xFF};

        /// <summary>
        /// Encodes Z85 string
        /// </summary>
        /// <param name="data">Data</param>
        private string Z85Encode(byte[] data)
        {
            byte byte_nbr = 0;
            UInt32 value = 0;
            string? dest = null;
            while (byte_nbr<data.Length) 
            {
                //  Accumulate value in base 256 (binary)
                value = value* 256 + data[byte_nbr++];
                if (byte_nbr % 4 == 0) 
                {
                    //  Output value in base 85
                    UInt32 divisor = 85 * 85 * 85 * 85;
                    while (divisor != 0) 
                    {
                        dest += Encoder[(int)(value / divisor % 85)];
                        divisor /= 85;
                    }
                    value = 0;
                }
            }
            return dest ?? "";
        }

        /// <summary>
        /// Decodes Z85 string
        /// </summary>
        /// <param name="key">key in Z85 format</param>
        /// <exception cref="ArgumentException">If key in invalid</exception>
        private byte[] Z85Decode(string key)
        {
            UInt32 char_nbr = 0;
            UInt32 value = 0;
            var dest_ = new List<byte>();
            foreach (var cha in key)
            {
                //  Accumulate value in base 85
                if (UInt32.MaxValue / 85 < value)
                {
                    //  Invalid z85 encoding, represented value exceeds 0xffffffff
                    throw new ArgumentException("Invalid key bad encoding");
                }
                value *= 85;
                char_nbr++;
                var index = cha - 32;
                if (index >= Decoder.Length)
                {
                    //  Invalid z85 encoding, character outside range
                    throw new ArgumentException("Invalid key character");
                }
                UInt32 summand = Decoder[index];
                if (summand == 0xFF || summand > (UInt32.MaxValue - value))
                {
                    //  Invalid z85 encoding, invalid character or represented value exceeds 0xffffffff
                    throw new ArgumentException("Invalid key character");
                }
                value += summand;
                if (char_nbr % 5 == 0)
                {
                    //  Output value in base 256
                    UInt32 divisor = 256 * 256 * 256;
                    while (divisor != 0)
                    {
                        dest_.Add((byte)(value / divisor % 256));
                        divisor /= 256;
                    }
                    value = 0;
                }
            }
            return dest_.ToArray();
        }

        /// <summary>
        /// Create a Certificate with a random secret key and a derived public key for the curve encryption
        /// </summary>
        public NetMQCertificate()
        {
            SecretKey = new byte[32];
            PublicKey = new byte[32];
            Curve25519XSalsa20Poly1305.KeyPair(SecretKey, PublicKey);
        }

        /// <summary>
        /// Create a certificate from secret key and public key
        /// </summary>
        /// <param name="secretKey">Secret key</param>
        /// <param name="publicKey">Public key</param>
        /// <exception cref="ArgumentException">If secretKey or publicKey are not 32-bytes long</exception>
        public NetMQCertificate(byte[] secretKey, byte[] publicKey)
        {
            if (secretKey.Length != 32)
                throw new ArgumentException("secretKey must be 32 bytes length");
            
            if (publicKey.Length != 32)
                throw new ArgumentException("publicKey must be 32 bytes length");
            
            SecretKey = secretKey;
            PublicKey = publicKey;
        }

        /// <summary>
        /// Create a certificate from secret key and public key
        /// </summary>
        /// <param name="secretKey">Secret key</param>
        /// <param name="publicKey">Public key</param>
        /// <exception cref="ArgumentException">If secretKey or publicKey are not 40-chars long</exception>
        public NetMQCertificate(string secretKey, string publicKey)
        {
            if (secretKey.Length != 40)
                throw new ArgumentException("secretKey must be 40 char long");

            if (publicKey.Length != 40)
                throw new ArgumentException("publicKey must be 40 char long");

            SecretKey = Z85Decode(secretKey);
            PublicKey = Z85Decode(publicKey);
        }

        private NetMQCertificate(byte[] key, bool isSecret)
        {
            if (key.Length != 32)
                throw new ArgumentException("key must be 32 bytes length");

            if (isSecret)
            {
                SecretKey = key;
                PublicKey = Curve25519.ScalarMultiplicationBase(key);
            }
            else
                PublicKey = key;
        }

        private NetMQCertificate(string keystr, bool isSecret)
        {
            if (keystr.Length != 40)
                throw new ArgumentException("key must be 40 bytes length");

            var key = Z85Decode(keystr);
            if (isSecret)
            {
                SecretKey = key;
                PublicKey = Curve25519.ScalarMultiplicationBase(key);
            }
            else
                PublicKey = key;
        }

        /// <summary>
        /// Create a certificate from secret key, public key is derived from the secret key
        /// </summary>
        /// <param name="secretKey">Secret Key</param>
        /// <exception cref="ArgumentException">If secret key is not 32-bytes long</exception>
        /// <returns>The newly created certificate</returns>
        public static NetMQCertificate CreateFromSecretKey(byte[] secretKey)
        {
            return new NetMQCertificate(secretKey, true);
        }

        /// <summary>
        /// Create a certificate from secret key, public key is derived from the secret key
        /// </summary>
        /// <param name="secretKey">Secret Key</param>
        /// <exception cref="ArgumentException">If secret key is not 32-bytes long</exception>
        /// <returns>The newly created certificate</returns>
        public NetMQCertificate FromSecretKey(byte[] secretKey)
        {
            return new NetMQCertificate(secretKey, true);
        }

        /// <summary>
        /// Create a certificate from secret key, public key is derived from the secret key
        /// </summary>
        /// <param name="secretKey">Secret Key</param>
        /// <exception cref="ArgumentException">If secret key is not 40-chars long</exception>
        /// <returns>The newly created certificate</returns>
        public static NetMQCertificate CreateFromSecretKey(string secretKey)
        {
            return new NetMQCertificate(secretKey, true);
        }

        /// <summary>
        /// Create a certificate from secret key, public key is derived from the secret key
        /// </summary>
        /// <param name="secretKey">Secret Key</param>
        /// <exception cref="ArgumentException">If secret key is not 40-chars long</exception>
        /// <returns>The newly created certificate</returns>
        public NetMQCertificate FromSecretKey(string secretKey)
        {
            return new NetMQCertificate(secretKey, true);
        }

        /// <summary>
        /// Create a public key only certificate.
        /// </summary>
        /// <param name="publicKey">Public key</param>
        /// <exception cref="ArgumentException">If public key is not 32-bytes long</exception>
        /// <returns>The newly created certificate</returns>
        public static NetMQCertificate FromPublicKey(byte[] publicKey)
        {
            return new NetMQCertificate(publicKey, false);
        }

        /// <summary>
        /// Create a public key only certificate.
        /// </summary>
        /// <param name="publicKey">Public key</param>
        /// <exception cref="ArgumentException">If public key is not 40-chars long</exception>
        /// <returns>The newly created certificate</returns>
        public static NetMQCertificate FromPublicKey(string publicKey)
        {
            return new NetMQCertificate(publicKey, false);
        }

        /// <summary>
        /// Curve Secret key
        /// </summary>
        public byte[]? SecretKey { get; private set; }


        /// <summary>
        /// Curve Secret key, encoded.
        /// </summary>
        public string? SecretKeyZ85 => SecretKey != null ? Z85Encode(SecretKey) : null;


        /// <summary>
        /// Returns true if the certificate also includes a secret key
        /// </summary>
        public bool HasSecretKey => SecretKey != null;
        
        /// <summary>
        /// Curve Public key.
        /// </summary>
        public byte[] PublicKey { get; private set; }

        /// <summary>
        /// Curve Public key, encoded.
        /// </summary>
        public string PublicKeyZ85 => Z85Encode(PublicKey);
    }
}