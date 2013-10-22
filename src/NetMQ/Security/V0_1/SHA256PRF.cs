using System;
using System.Collections.Generic;
using System.Linq;
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
    byte[] Get(byte[] secret, string label, byte[] seed, int bytes);    
  }

  public class SHA256PRF : IPRF
  {
    public byte[] Get(byte[] secret, string label, byte[] seed, int bytes)
    {
      byte[] prf = PRF(secret, label, seed, (bytes/32) + 1);

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

    public void Dispose()
    {
      
    }
  }
}
