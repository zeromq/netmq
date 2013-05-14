using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NetMQ.Security.V0_1
{
  class RecordLayer : IDisposable
  {
    private const string KeyExpansion = "key expansion";

    public const int WindowSize = 1024;

    private SymmetricAlgorithm m_decryptionBulkAlgorithm;
    private SymmetricAlgorithm m_encryptionBulkAlgorithm;

    private HMAC m_decryptionHMAC;
    private HMAC m_encryptionHMAC;

    private ulong m_sequenceNumber = 0;

    private readonly byte[] m_protocolVersion;

    private ulong m_leftWindow = 0;
    private ulong m_rightWindow = WindowSize - 1;
    private bool[] m_windowMap = new bool[WindowSize];

    public RecordLayer(byte[] protocolVersion)
    {
      m_protocolVersion = protocolVersion;

      SecurityParameters = new SecurityParameters();
      SecurityParameters.BulkCipherAlgorithm = BulkCipherAlgorithm.Null;
      SecurityParameters.MACAlgorithm = MACAlgorithm.Null;  

      PRF  = new SHA256PRF();
    }

    public SecurityParameters SecurityParameters { get;  set; }

    public IPRF PRF { get; set; }

    private void GenerateKeys(
      out byte[] clientMAC, out byte[] serverMAC,
      out byte[] clientEncryptionKey, out byte[] serverEncryptionKey)
    {
      byte[] seed = new byte[HandshakeLayer.RandomNumberLength * 2];

      Buffer.BlockCopy(SecurityParameters.ServerRandom, 0, seed, 0, HandshakeLayer.RandomNumberLength);
      Buffer.BlockCopy(SecurityParameters.ClientRandom, 0, seed,
        HandshakeLayer.RandomNumberLength, HandshakeLayer.RandomNumberLength);

      int length = (SecurityParameters.FixedIVLength +
                    SecurityParameters.EncKeyLength + SecurityParameters.MACKeyLength) * 2;

      if (length > 0)
      {
        byte[] keyBlock = PRF.Get(SecurityParameters.MasterSecret,
                                                   KeyExpansion, seed, length);

        clientMAC = new byte[SecurityParameters.MACKeyLength];
        Buffer.BlockCopy(keyBlock, 0, clientMAC, 0, SecurityParameters.MACKeyLength);
        int pos = SecurityParameters.MACKeyLength;

        serverMAC = new byte[SecurityParameters.MACKeyLength];
        Buffer.BlockCopy(keyBlock, pos, serverMAC, 0, SecurityParameters.MACKeyLength);
        pos += SecurityParameters.MACKeyLength;

        clientEncryptionKey = new byte[SecurityParameters.EncKeyLength];
        Buffer.BlockCopy(keyBlock, pos, clientEncryptionKey, 0, SecurityParameters.EncKeyLength);
        pos += SecurityParameters.EncKeyLength;

        serverEncryptionKey = new byte[SecurityParameters.EncKeyLength];
        Buffer.BlockCopy(keyBlock, pos, serverEncryptionKey, 0, SecurityParameters.EncKeyLength);
      }
      else
      {
        clientMAC = serverMAC = clientEncryptionKey = serverEncryptionKey = null;
      }
    }

    public void InitalizeCipherSuite()
    {
      byte[] clientMAC;
      byte[] serverMAC;
      byte[] clientEncryptionKey;
      byte[] serverEncryptionKey;

      GenerateKeys(out clientMAC, out serverMAC, out clientEncryptionKey, out serverEncryptionKey);

      if (SecurityParameters.BulkCipherAlgorithm == BulkCipherAlgorithm.AES)
      {
        m_decryptionBulkAlgorithm = new AesCryptoServiceProvider();
        m_decryptionBulkAlgorithm.Padding = PaddingMode.None;
        m_decryptionBulkAlgorithm.KeySize = SecurityParameters.EncKeyLength * 8;
        m_decryptionBulkAlgorithm.BlockSize = SecurityParameters.BlockLength * 8;

        m_encryptionBulkAlgorithm = new AesCryptoServiceProvider();
        m_encryptionBulkAlgorithm.Padding = PaddingMode.None;
        m_encryptionBulkAlgorithm.KeySize = SecurityParameters.EncKeyLength * 8;
        m_encryptionBulkAlgorithm.BlockSize = SecurityParameters.BlockLength * 8;

        if (SecurityParameters.Entity == ConnectionEnd.Client)
        {
          m_encryptionBulkAlgorithm.Key = clientEncryptionKey;
          m_decryptionBulkAlgorithm.Key = serverEncryptionKey;
        }
        else
        {
          m_decryptionBulkAlgorithm.Key = clientEncryptionKey;
          m_encryptionBulkAlgorithm.Key = serverEncryptionKey;
        }
      }
      else
      {
        m_decryptionBulkAlgorithm = m_encryptionBulkAlgorithm = null;
      }

      if (SecurityParameters.MACAlgorithm == MACAlgorithm.HMACSha1)
      {
        if (SecurityParameters.Entity == ConnectionEnd.Client)
        {
          m_encryptionHMAC = new HMACSHA1(clientMAC);
          m_decryptionHMAC = new HMACSHA1(serverMAC);
        }
        else
        {
          m_encryptionHMAC = new HMACSHA1(serverMAC);
          m_decryptionHMAC = new HMACSHA1(clientMAC);
        }
      }
      else if (SecurityParameters.MACAlgorithm == MACAlgorithm.HMACSha256)
      {
        if (SecurityParameters.Entity == ConnectionEnd.Client)
        {
          m_encryptionHMAC = new HMACSHA256(clientMAC);
          m_decryptionHMAC = new HMACSHA256(serverMAC);
        }
        else
        {
          m_encryptionHMAC = new HMACSHA256(serverMAC);
          m_decryptionHMAC = new HMACSHA256(clientMAC);
        }
      }
      else
      {
        m_encryptionHMAC = m_decryptionHMAC = null;
      }
    }

    public NetMQMessage EncryptMessage(ContentType contentType, NetMQMessage plainMessage)
    {
      if (SecurityParameters.BulkCipherAlgorithm == BulkCipherAlgorithm.Null &&
        SecurityParameters.MACAlgorithm == MACAlgorithm.Null)
      {
        return plainMessage;
      }

      NetMQMessage cipherMessage = new NetMQMessage();

      // generate new iv
      m_encryptionBulkAlgorithm.GenerateIV();
      cipherMessage.Append(m_encryptionBulkAlgorithm.IV);

      using (var encryptor = m_encryptionBulkAlgorithm.CreateEncryptor())
      {
        ulong seqNum = GetAndIncreaseSequneceNumber();

        byte[] seqNumBytes = BitConverter.GetBytes(seqNum);

        int frameIndex = 0;

        byte[] cipherSeqNumBytes = EncryptBytes(encryptor, contentType, seqNum, frameIndex, seqNumBytes);
        cipherMessage.Append(cipherSeqNumBytes);

        frameIndex++;

        foreach (NetMQFrame plainFrame in plainMessage)
        {
          byte[] cipherBytes = EncryptBytes(encryptor, contentType, seqNum, frameIndex, plainFrame.ToByteArray());
          cipherMessage.Append(cipherBytes);

          frameIndex++;
        }

        return cipherMessage;
      }
    }

    private ulong GetAndIncreaseSequneceNumber()
    {
      return m_sequenceNumber++;
    }

    private byte[] EncryptBytes(ICryptoTransform encryptor, ContentType contentType, ulong seqNum,
      int frameIndex, byte[] plainBytes)
    {
      byte[] mac;

      if (SecurityParameters.MACAlgorithm != MACAlgorithm.Null)
      {
        byte[] versionAndType = new byte[] { (byte)contentType, m_protocolVersion[0], m_protocolVersion[1] };
        byte[] seqNumBytes = BitConverter.GetBytes(seqNum);
        byte[] messageSize = BitConverter.GetBytes(plainBytes.Length);
        byte[] frameIndexBytes = BitConverter.GetBytes(frameIndex);

        m_encryptionHMAC.Initialize();
        m_encryptionHMAC.TransformBlock(seqNumBytes, 0, seqNumBytes.Length, seqNumBytes, 0);
        m_encryptionHMAC.TransformBlock(versionAndType, 0, versionAndType.Length, versionAndType, 0);
        m_encryptionHMAC.TransformBlock(messageSize, 0, messageSize.Length, messageSize, 0);
        m_encryptionHMAC.TransformBlock(frameIndexBytes, 0, frameIndexBytes.Length, frameIndexBytes, 0);
        m_encryptionHMAC.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        mac = m_encryptionHMAC.Hash;
      }
      else
      {
        mac = new byte[0];
      }

      int length = plainBytes.Length + SecurityParameters.MACLength;
      byte padding = 0;

      if (SecurityParameters.BulkCipherAlgorithm != null)
      {
        padding = (byte)((encryptor.OutputBlockSize -
                                (plainBytes.Length + SecurityParameters.MACLength + 1) % encryptor.OutputBlockSize) %
                               encryptor.OutputBlockSize);

        length += padding + 1;
      }

      byte[] cipherBytes = new byte[length];

      Buffer.BlockCopy(plainBytes, 0, cipherBytes, 0, plainBytes.Length);
      Buffer.BlockCopy(mac, 0, cipherBytes, plainBytes.Length, SecurityParameters.MACLength);

      if (SecurityParameters.BulkCipherAlgorithm != BulkCipherAlgorithm.Null)
      {
        for (int i = plainBytes.Length + SecurityParameters.MACLength; i < cipherBytes.Length; i++)
        {
          cipherBytes[i] = padding;
        }

        encryptor.TransformBlock(cipherBytes, 0, cipherBytes.Length, cipherBytes, 0);
      }

      return cipherBytes;
    }

    public NetMQMessage DecryptMessage(ContentType contentType, NetMQMessage cipherMessage)
    {
      if (SecurityParameters.BulkCipherAlgorithm == BulkCipherAlgorithm.Null &&
        SecurityParameters.MACAlgorithm == MACAlgorithm.Null)
      {
        return cipherMessage;
      }

      NetMQFrame ivFrame = cipherMessage.Pop();

      m_decryptionBulkAlgorithm.IV = ivFrame.ToByteArray();

      using (var decryptor = m_decryptionBulkAlgorithm.CreateDecryptor())
      {
        NetMQMessage plainMessage = new NetMQMessage();

        NetMQFrame seqNumFrame = cipherMessage.Pop();

        byte[] seqNumBytes;
        byte[] seqNumMAC;

        DecryptBytes(decryptor, seqNumFrame.ToByteArray(), out seqNumBytes, out seqNumMAC);

        ulong seqNum = BitConverter.ToUInt64(seqNumBytes, 0);

        int frameIndex = 0;

        ValidateBytes(contentType, seqNum, frameIndex, seqNumBytes, seqNumMAC);

        if (CheckReplayAttack(seqNum))
        {
          // under reply attack, silently drop the message and return null
          return null;
        }

        frameIndex++;

        foreach (NetMQFrame cipherFrame in cipherMessage)
        {
          byte[] data;
          byte[] mac;

          DecryptBytes(decryptor, cipherFrame.ToByteArray(), out data, out mac);
          ValidateBytes(contentType, seqNum, frameIndex, data, mac);

          frameIndex++;

          plainMessage.Append(data);
        }

        return plainMessage;
      }
    }

    private void DecryptBytes(ICryptoTransform decryptor, byte[] cipherBytes, out byte[] plainBytes, out byte[] mac)
    {
      byte[] frameBytes = new byte[cipherBytes.Length];

      int dataLength;

      if (SecurityParameters.BulkCipherAlgorithm != BulkCipherAlgorithm.Null)
      {
        decryptor.TransformBlock(cipherBytes, 0, cipherBytes.Length, frameBytes, 0);

        int padding = frameBytes[frameBytes.Length - 1];

        dataLength = frameBytes.Length - 1 - padding - SecurityParameters.MACLength;
      }
      else
      {
        dataLength = frameBytes.Length - SecurityParameters.MACLength;
        frameBytes = cipherBytes;
      }

      plainBytes = new byte[dataLength];
      Buffer.BlockCopy(frameBytes, 0, plainBytes, 0, dataLength);

      mac = new byte[SecurityParameters.MACLength];
      Buffer.BlockCopy(frameBytes, dataLength, mac, 0, SecurityParameters.MACLength);
    }

    public void ValidateBytes(ContentType contentType, ulong seqNum, int frameIndex, byte[] plainBytes, byte[] mac)
    {
      if (SecurityParameters.MACAlgorithm != MACAlgorithm.Null)
      {
        byte[] versionAndType = new byte[] { (byte)contentType, m_protocolVersion[0], m_protocolVersion[1] };
        byte[] seqNumBytes = BitConverter.GetBytes(seqNum);
        byte[] messageSize = BitConverter.GetBytes(plainBytes.Length);
        byte[] frameIndexBytes = BitConverter.GetBytes(frameIndex);

        m_decryptionHMAC.Initialize();
        m_decryptionHMAC.TransformBlock(seqNumBytes, 0, seqNumBytes.Length, seqNumBytes, 0);
        m_decryptionHMAC.TransformBlock(versionAndType, 0, versionAndType.Length, versionAndType, 0);
        m_decryptionHMAC.TransformBlock(messageSize, 0, messageSize.Length, messageSize, 0);
        m_decryptionHMAC.TransformBlock(frameIndexBytes, 0, frameIndexBytes.Length, frameIndexBytes, 0);
        m_decryptionHMAC.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        if (!m_decryptionHMAC.Hash.SequenceEqual(mac))
        {
          throw new NetMQSecurityException("MAC not matched message");
        }
      }
    }

    private bool CheckReplayAttack(ulong seqNumber)
    {
      if (seqNumber < m_leftWindow)
      {
        return true;
      }
      else if (seqNumber <= m_rightWindow)
      {
        int index = (int)(seqNumber % WindowSize);

        if (!m_windowMap[index])
        {
          m_windowMap[index] = true;
          return false;
        }
        else
        {
          return true;
        }
      }
      else
      {
        // if new seq is much higher than the window size somebody is trying to do a reply attack as well
        if (seqNumber - m_rightWindow > WindowSize - 1)
        {
          return true;
        }

        // need to extend window size
        ulong bytesToExtend = seqNumber - m_rightWindow;
        
        // set to false the new extension
        for (ulong i = 0; i < bytesToExtend; i++)
        {
          int index = (int)((m_leftWindow + i) % WindowSize);

          m_windowMap[index] = false;
        }

        m_leftWindow = m_leftWindow + bytesToExtend;
        m_rightWindow = seqNumber;

        return false;
      }
    }

    public void Dispose()
    {
      m_decryptionBulkAlgorithm.Dispose();
      m_encryptionBulkAlgorithm.Dispose();
      m_decryptionHMAC.Dispose();
      m_encryptionHMAC.Dispose();
      PRF.Dispose();
    }
  }
}
