#nullable enable

using System;
using NaCl;

namespace NetMQ
{
    /// <summary>
    /// Certificate to be used for the Curve encryption
    /// </summary>
    public class NetMQCertificate
    {
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
        /// Curve Secret key
        /// </summary>
        public byte[]? SecretKey { get; private set; }

        /// <summary>
        /// Returns true if the certificate also includes a secret key
        /// </summary>
        public bool HasSecretKey => SecretKey != null;
        
        /// <summary>
        /// Curve Public key 
        /// </summary>
        public byte[] PublicKey { get; private set; }
    }
}