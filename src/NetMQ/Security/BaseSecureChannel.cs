using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NetMQ.Security
{
  public enum BulkCipherAlgorithm
  {
    AES128, AES256
  }

  public enum MACAlgorithm
  {
    SHA1, SHA256
  }  

  public abstract class BaseSecureChannel 
  {
    public const int RandomBytesLength = 32;
    public const int PreMasterSecretLength = 48;
    public const int MasterSecretLength = 48;
    public const int WindowSize = 1024;
    public const int VerifyDataSize = 12;

    private byte[] m_incomingKey;
    private byte[] m_outgoingKey;

    private byte[] m_incomingHMACKey;
    private byte[] m_outgoingHMACKey;

    private byte[] m_masterSecret;

    private SymmetricAlgorithm m_encryptorSymmetricAlgorithm;
    private SymmetricAlgorithm m_decryptorSymmetricAlgorithm;

    private HMAC m_encryptorHMAC;
    private HMAC m_decryptorHMAC;

    private RandomNumberGenerator m_rng;
    private byte[] m_clientRandomNumber;
    private byte[] m_serverRandomNumber;

    private BulkCipherAlgorithm m_bulkCipherAlgorithm;
    private MACAlgorithm m_macAlgorithm;

    private ulong m_sequenceNumber = 0;

    private MemoryStream m_macMemoryStream;

    private MemoryStream m_handshakeMemoryStream;  

    private ulong m_leftWindow = 0;
    private ulong m_rightWindow = WindowSize - 1;
    private bool[] m_windowBitsMap = new bool[WindowSize];

    public BaseSecureChannel()
    {
      m_rng = new RNGCryptoServiceProvider();
      MACAlgorithm = MACAlgorithm.SHA1;
      BulkCipherAlgorithm = BulkCipherAlgorithm.AES128;

      m_macMemoryStream = new MemoryStream(1024 * 10); // initialize the size to 10KB      
      m_handshakeMemoryStream = new MemoryStream(1024 * 10);
    }

    public BulkCipherAlgorithm BulkCipherAlgorithm
    {
      get
      {
        return m_bulkCipherAlgorithm;
      }
      set
      {
        m_bulkCipherAlgorithm = value;

        if (BulkCipherAlgorithm == BulkCipherAlgorithm.AES128)
        {
          SymmetricKeyLengthBytes = 16;
          IVSizeBytes = 16;
        }
        else if (BulkCipherAlgorithm == BulkCipherAlgorithm.AES256)
        {
          IVSizeBytes = 16;
          SymmetricKeyLengthBytes = 32;
        }
      }
    }

    public MACAlgorithm MACAlgorithm
    {
      get
      {
        return m_macAlgorithm;
      }
      set
      {
        m_macAlgorithm = value;

        if (MACAlgorithm == MACAlgorithm.SHA1)
        {
          MACKeyLengthBytes = 20;
        }
        else if (MACAlgorithm == MACAlgorithm.SHA256)
        {
          MACKeyLengthBytes = 32;
        }
      }
    }

    protected byte[] ClientRandomNumber
    {
      get { return m_clientRandomNumber; }
      set
      {
        if (value.Length != RandomBytesLength)
        {
          throw new InvalidException("Client Random Number length is wrong");
        }

        m_clientRandomNumber = value;
      }
    }

    protected byte[] ServerRandomNumber
    {
      get { return m_serverRandomNumber; }
      set
      {
        if (value.Length != RandomBytesLength)
        {
          throw new InvalidException("Server Random Number length is wrong");
        }

        m_serverRandomNumber = value;
      }
    }

    public int SymmetricKeyLengthBytes { get; private set; }

    public int SymmetricKeyLength
    {
      get { return SymmetricKeyLengthBytes * 8; }
    }

    public int MACKeyLengthBytes { get; private set; }

    public int MACKeyLength
    {
      get { return MACKeyLengthBytes * 8; }
    }

    public int IVSizeBytes { get; private set; }

    private void GenerateMasterSecret(byte[] preMasterSecret)
    {
      byte[] seed = new byte[RandomBytesLength * 2];

      Buffer.BlockCopy(ClientRandomNumber, 0, seed, 0, RandomBytesLength);
      Buffer.BlockCopy(ServerRandomNumber, 0, seed, RandomBytesLength, RandomBytesLength);

      m_masterSecret = new byte[MasterSecretLength];

      byte[] prf = PRF(preMasterSecret, "master secret", seed, 2);

      Buffer.BlockCopy(prf, 0, m_masterSecret, 0, MasterSecretLength);

      // clear the pre master secret
      for (int i = 0; i < preMasterSecret.Length; i++)
      {
        preMasterSecret[i] = 0;
      }
    }

    protected void InitializeCipherSuite(byte[] preMasterSecret, bool client)
    {
      GenerateMasterSecret(preMasterSecret);
      GenerateKeys(client);
      InitializeBulkCipherAlgorithm();
      InitializeHMACAlgorithm();

      CipherSuiteInitialized = true;
    }

    protected bool CipherSuiteInitialized { get; private set; }

    private void GenerateKeys(bool client)
    {
      int size = (MACKeyLengthBytes + SymmetricKeyLengthBytes) * 2;

      byte[] keyBlock = GenerateKeyBlock(m_masterSecret, ClientRandomNumber, ServerRandomNumber, size);

      int incomingHMACKeyLocation = 0;
      int outgoingHMACKeyLocation = MACKeyLengthBytes;

      int incomingKeyLocation = outgoingHMACKeyLocation + MACKeyLengthBytes;
      int outgoingKeyLocation = incomingKeyLocation + SymmetricKeyLengthBytes;

      if (client)
      {
        outgoingHMACKeyLocation = 0;
        incomingHMACKeyLocation = MACKeyLengthBytes;

        outgoingKeyLocation = incomingHMACKeyLocation + MACKeyLengthBytes;
        incomingKeyLocation = outgoingKeyLocation + SymmetricKeyLengthBytes;
      }

      m_incomingHMACKey = new byte[MACKeyLengthBytes];
      Buffer.BlockCopy(keyBlock, incomingHMACKeyLocation, m_incomingHMACKey, 0, MACKeyLengthBytes);

      m_outgoingHMACKey = new byte[MACKeyLengthBytes];
      Buffer.BlockCopy(keyBlock, outgoingHMACKeyLocation, m_outgoingHMACKey, 0, MACKeyLengthBytes);

      m_incomingKey = new byte[SymmetricKeyLengthBytes];
      Buffer.BlockCopy(keyBlock, incomingKeyLocation, m_incomingKey, 0, SymmetricKeyLengthBytes);

      m_outgoingKey = new byte[SymmetricKeyLengthBytes];
      Buffer.BlockCopy(keyBlock, outgoingKeyLocation, m_outgoingKey, 0, SymmetricKeyLengthBytes);
    }

    private void InitializeBulkCipherAlgorithm()
    {
      m_encryptorSymmetricAlgorithm = new AesCryptoServiceProvider();
      m_encryptorSymmetricAlgorithm.KeySize = SymmetricKeyLength;
      m_encryptorSymmetricAlgorithm.Key = m_outgoingKey;

      m_decryptorSymmetricAlgorithm = new AesCryptoServiceProvider();
      m_decryptorSymmetricAlgorithm.KeySize = SymmetricKeyLength;
      m_decryptorSymmetricAlgorithm.Key = m_incomingKey;
    }

    private void InitializeHMACAlgorithm()
    {
      if (MACAlgorithm == MACAlgorithm.SHA1)
      {
        m_decryptorHMAC = new HMACSHA1(m_incomingHMACKey);
        m_encryptorHMAC = new HMACSHA1(m_outgoingHMACKey);
      }
      else
      {
        m_decryptorHMAC = new HMACSHA256(m_incomingHMACKey);
        m_encryptorHMAC = new HMACSHA256(m_outgoingHMACKey);
      }
    }

    private static byte[] GenerateKeyBlock(byte[] masterSecret, byte[] clientRandom, byte[] serverRandom, int length)
    {
      byte[] seed = new byte[RandomBytesLength * 2];

      Buffer.BlockCopy(serverRandom, 0, seed, 0, RandomBytesLength);
      Buffer.BlockCopy(clientRandom, 0, seed, RandomBytesLength, RandomBytesLength);

      byte[] keyBlock = new byte[length];

      byte[] prf = PRF(masterSecret, "key expansion", seed, (length / 32) + 1);

      Buffer.BlockCopy(prf, 0, keyBlock, 0, length);

      return keyBlock;
    }

    protected byte[] GenerateVerifyData(string label)
    {
      using (var sha256 = SHA256.Create())
      {
        m_handshakeMemoryStream.Position = 0;

        byte[] seed = sha256.ComputeHash(m_handshakeMemoryStream);

        byte[]prf =PRF(m_masterSecret, label, seed, 1);

        byte[] verifyData = new byte[VerifyDataSize];

        Buffer.BlockCopy(prf, 0, verifyData, 0, VerifyDataSize);
        
        return verifyData;
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

        byte[] prf = new byte[iterations*32];

        byte[] buffer = new byte[32 + seed.Length];
        Buffer.BlockCopy(seed, 0, buffer, 32, seed.Length);

        for (int i = 0; i < iterations; i++)
        {
          Buffer.BlockCopy(a[i + 1], 0, buffer, 0, 32);

          byte[] hash = hmac.ComputeHash(buffer);

          Buffer.BlockCopy(hash, 0, prf, 32*i, 32);
        }

        return prf;
      }
    }

    protected byte[] GenerateRandomNumber()
    {
      byte[] randomNumber = new byte[RandomBytesLength];
      m_rng.GetBytes(randomNumber);

      return randomNumber;
    }

    protected byte[] GeneratePreMasterSecret()
    {
      byte[] preMasterSecret = new byte[PreMasterSecretLength];
      m_rng.GetBytes(preMasterSecret);

      return preMasterSecret;
    }

    public NetMQMessage EncryptMessage(NetMQMessage message)
    {
      NetMQMessage encryptedMessage = new NetMQMessage();

      // first frame is the message IV
      m_encryptorSymmetricAlgorithm.GenerateIV();
      byte[] iv = m_encryptorSymmetricAlgorithm.IV;
      encryptedMessage.Append(iv);

      m_macMemoryStream.Position = 0;
      m_macMemoryStream.SetLength(0);

      // set the sequence number
      ulong seqNumber = m_sequenceNumber++;
      byte[] seqNumberBytes = BitConverter.GetBytes(seqNumber);

      // add the sequence numbet to the mac memory stream                      
      m_macMemoryStream.Write(seqNumberBytes, 0, seqNumberBytes.Length);

      // encrypt the sequence number and add to the message
      byte[] encryptedSeqNumber = Encrypt(seqNumberBytes, seqNumberBytes.Length);
      encryptedMessage.Append(encryptedSeqNumber);

      foreach (var frame in message)
      {
        // add the plain frame to the memory stream
        m_macMemoryStream.Write(frame.Buffer, 0, frame.MessageSize);

        byte[] cipherData = Encrypt(frame.Buffer, frame.MessageSize);

        encryptedMessage.Append(cipherData);
      }

      m_macMemoryStream.Position = 0;

      // compute the mac 
      byte[] plainMAC = m_encryptorHMAC.ComputeHash(m_macMemoryStream);

      // encrypt the mac and add to the message
      byte[] encrypedMAC = Encrypt(plainMAC, plainMAC.Length);
      encryptedMessage.Append(encrypedMAC);

      return encryptedMessage;
    }

    public NetMQMessage DecryptMessage(NetMQMessage message)
    {
      NetMQMessage plainMessage = new NetMQMessage();

      byte[] iv = message[0].Buffer;

      if (message[0].BufferSize > message[0].MessageSize)
      {
        iv = new byte[message[0].MessageSize];

        Buffer.BlockCopy(message[0].Buffer, 0, iv, 0, message[0].MessageSize);
      }

      m_decryptorSymmetricAlgorithm.IV = iv;

      m_macMemoryStream.Position = 0;
      m_macMemoryStream.SetLength(0);

      // decrypt the sequnece number
      byte[] plainSeqNumberBytes = Decrypt(message[1].Buffer, message[1].BufferSize);
      ulong seqNumber = BitConverter.ToUInt64(plainSeqNumberBytes, 0);

      if (CheckReplyAttack(seqNumber))
      {
        // under reply attack, silently drop the message and return null
        return null;
      }

      m_macMemoryStream.Write(plainSeqNumberBytes, 0, plainSeqNumberBytes.Length);

      for (int i = 2; i < message.FrameCount - 1; i++)
      {
        NetMQFrame frame = message[i];

        byte[] plainData = Decrypt(frame.Buffer, frame.MessageSize);
        m_macMemoryStream.Write(plainData, 0, plainData.Length);

        plainMessage.Append(plainData);
      }

      m_macMemoryStream.Position = 0;

      byte[] computeMAC = m_decryptorHMAC.ComputeHash(m_macMemoryStream);
      byte[] plainMAC = Decrypt(message.Last.Buffer, message.Last.MessageSize);

      if (computeMAC.Length != plainMAC.Length)
      {
        // mac is wrong, silently droping the message and return null
        return null;
      }

      for (int i = 0; i < computeMAC.Length; i++)
      {
        if (computeMAC[i] != plainMAC[i])
        {
          // mac is wrong, silently droping the message and return null
          return null;
        }
      }

      return plainMessage;
    }

    private bool CheckReplyAttack(ulong seqNumber)
    {
      if (seqNumber < m_leftWindow)
      {
        return true;
      }
      else if (seqNumber <= m_rightWindow)
      {
        int index = (int)(seqNumber%WindowSize);

        if (!m_windowBitsMap[index])
        {
          m_windowBitsMap[index] = true;
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

          m_windowBitsMap[index] = false;
        }

        m_leftWindow = m_leftWindow + bytesToExtend;
        m_rightWindow = seqNumber;

        return false;
      }      
    }

    private byte[] Encrypt(byte[] plain, int plainSize)
    {
      using (var encryptor = m_encryptorSymmetricAlgorithm.CreateEncryptor())
      {
        MemoryStream memoryStream = new MemoryStream(plainSize + encryptor.OutputBlockSize);

        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
        {
          cryptoStream.Write(plain, 0, plainSize);
        }

        byte[] cipher = memoryStream.ToArray();

        // take the last block as the IV for the next frame
        byte[] newIV = new byte[IVSizeBytes];
        Buffer.BlockCopy(cipher, cipher.Length - IVSizeBytes, newIV, 0, IVSizeBytes);
        m_encryptorSymmetricAlgorithm.IV = newIV;

        return cipher;
      }
    }

    private byte[] Decrypt(byte[] cipher, int cipherSize)
    {
      using (var decryptor = m_decryptorSymmetricAlgorithm.CreateDecryptor())
      {
        MemoryStream memoryStream = new MemoryStream(cipher, 0, cipherSize, false);

        byte[] plain = new byte[(cipherSize / decryptor.InputBlockSize) * decryptor.OutputBlockSize];

        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
        {
          int bytesRead = cryptoStream.Read(plain, 0, plain.Length);

          if (bytesRead != plain.Length)
          {
            byte[] temp = new byte[bytesRead];

            Buffer.BlockCopy(plain, 0, temp, 0, bytesRead);

            plain = temp;
          }
        }
        
        // take the last block as the IV for the next frame
        byte[] newIV = new byte[IVSizeBytes];
        Buffer.BlockCopy(cipher, cipher.Length - IVSizeBytes, newIV, 0, IVSizeBytes);
        m_decryptorSymmetricAlgorithm.IV = newIV;

        return plain;
      }

    }
      
    protected void WriteMessageToHandshakeStream(NetMQMessage message)
    {
      foreach (var frame in message)
      {
        m_handshakeMemoryStream.Write(frame.Buffer, 0, frame.MessageSize);
      }            
    }
  }

}
