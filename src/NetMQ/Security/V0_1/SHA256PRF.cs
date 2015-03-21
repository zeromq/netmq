using System;
using System.Security.Cryptography;
using System.Text;

namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// The IPRF interface specifies the method Get(byte[], string, byte[], int).
    /// PRF stands for Pseudo-Random number generating Function.
    /// </summary>
    public interface IPRF : IDisposable
    {
        /// <summary>
        /// Given a shared-secret, return a byte-array that comprises a random number.
        /// </summary>
        /// <param name="secret">the shared secret that is used in the generation of the random number</param>
        /// <param name="label"></param>
        /// <param name="seed">a "seed" value that affects the randomness of the number</param>
        /// <param name="bytes">the number of bytes to write in the result</param>
        /// <returns>a byte-array that comprises the random number</returns>
        byte[] Get(byte[] secret, string label, byte[] seed, int bytes);
    }

    /// <summary>
    /// Class SHA256PRF is a secure hashing algorithm. It implements the interface <see cref="IPRF"/>
    /// and uses the SHA256 hashing algorithm.
    /// </summary>
    /// <remarks>
    /// SHA-256 is a 256-bit hash and is meant to provide 128 bites of security against collision attacks.
    /// SHA stands for Secure Hashing Algorithm, and is a type of PRF.
    /// PRF stands for Pseudo-Random number generating Function, and <see cref="IPRF"/> is an interface that mandates the Get method.
    /// </remarks>
    public class SHA256PRF : IPRF
    {
        /// <summary>
        /// Given a shared-secret, return a byte-array that comprises a random number.
        /// </summary>
        /// <param name="secret">the shared secret that is used in the generation of the random number</param>
        /// <param name="label"></param>
        /// <param name="seed">a "seed" value that affects the randomness of the number</param>
        /// <param name="bytes">the number of bytes to write in the result</param>
        /// <returns>a byte-array that comprises the random number</returns>
        public byte[] Get(byte[] secret, string label, byte[] seed, int bytes)
        {
            byte[] prf = PRF(secret, label, seed, (bytes / 32) + 1);

            if (prf.Length == bytes)
            {
                return prf;
            }
            else
            {
                byte[] p = new byte[bytes];
                Buffer.BlockCopy(prf, 0, p, 0, bytes);

                return p;
            }
        }

        /// <summary>
        /// Combine the given secret and label into one "secret" and call PHash.
        /// </summary>
        /// <param name="secret">a shared secret</param>
        /// <param name="label">a string that will be combined with the secret and used as the combined shared-secret</param>
        /// <param name="seed">a seed value that affects the randomness of the result</param>
        /// <param name="iterations"></param>
        /// <returns>a byte-array that comprises the random number</returns>
        private static byte[] PRF(byte[] secret, string label, byte[] seed, int iterations)
        {
            byte[] ls = new byte[label.Length + seed.Length];

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(label), 0, ls, 0, label.Length);
            Buffer.BlockCopy(seed, 0, ls, label.Length, seed.Length);

            return PHash(secret, ls, iterations);
        }

        private static byte[] PHash(byte[] secret, byte[] seed, int iterations)
        {
            using (HMACSHA256 hmac = new HMACSHA256(secret))
            {
                byte[][] a = new byte[iterations + 1][];

                a[0] = seed;

                for (int i = 0; i < iterations; i++)
                {
                    a[i + 1] = hmac.ComputeHash(a[i]);
                }

                byte[] prf = new byte[iterations * 32];

                byte[] buffer = new byte[32 + seed.Length];
                Buffer.BlockCopy(seed, 0, buffer, 32, seed.Length);

                for (int i = 0; i < iterations; i++)
                {
                    Buffer.BlockCopy(a[i + 1], 0, buffer, 0, 32);

                    byte[] hash = hmac.ComputeHash(buffer);

                    Buffer.BlockCopy(hash, 0, prf, 32 * i, 32);
                }

                return prf;
            }
        }

        /// <summary>
        /// This does nothing.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// This does nothing.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
