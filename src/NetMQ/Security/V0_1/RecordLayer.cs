using System;
using System.Linq;
using System.Security.Cryptography;

namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// The RecordLayer class represents the "Record Layer" within the SSL/TLS protocol layers.
    /// This is underneath the Handshake Layer, and above the Transport Layer.
    /// </summary>
    /// <remarks>
    /// See http://technet.microsoft.com/en-us/library/cc781476(v=ws.10).aspx
    /// </remarks>
    internal class RecordLayer : IDisposable
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
        private readonly bool[] m_windowMap = new bool[WindowSize];

        /// <summary>
        /// Create a new RecordLayer object with the given protocol-version.
        /// </summary>
        /// <param name="protocolVersion">a 2-element byte-array that denotes the version of this protocol</param>
        public RecordLayer(byte[] protocolVersion)
        {
            m_protocolVersion = protocolVersion;

            SecurityParameters = new SecurityParameters
            {
                BulkCipherAlgorithm = BulkCipherAlgorithm.Null,
                MACAlgorithm = MACAlgorithm.Null
            };

            PRF = new SHA256PRF();
        }

        public SecurityParameters SecurityParameters { get; set; }

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
                m_decryptionBulkAlgorithm = new AesCryptoServiceProvider
                {
                    Padding = PaddingMode.None,
                    KeySize = SecurityParameters.EncKeyLength*8,
                    BlockSize = SecurityParameters.BlockLength*8
                };

                m_encryptionBulkAlgorithm = new AesCryptoServiceProvider
                {
                    Padding = PaddingMode.None,
                    KeySize = SecurityParameters.EncKeyLength*8,
                    BlockSize = SecurityParameters.BlockLength*8
                };

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

        /// <param name="contentType">This identifies the type of content: ChangeCipherSpec, Handshake, or ApplicationData.</param>
        /// <param name="plainMessage">The unencrypted form of the message to be encrypted.</param>
        public NetMQMessage EncryptMessage(ContentType contentType, NetMQMessage plainMessage)
        {
            if (SecurityParameters.BulkCipherAlgorithm == BulkCipherAlgorithm.Null &&
              SecurityParameters.MACAlgorithm == MACAlgorithm.Null)
            {
                return plainMessage;
            }

            NetMQMessage cipherMessage = new NetMQMessage();

            using (var encryptor = m_encryptionBulkAlgorithm.CreateEncryptor())
            {
                ulong seqNum = GetAndIncreaseSequneceNumber();
                byte[] seqNumBytes = BitConverter.GetBytes(seqNum);

                var iv = GenerateIV(encryptor, seqNumBytes);
                cipherMessage.Append(iv);

                // including the frame number in the message to make sure the frames are not reordered
                int frameIndex = 0;

                // the first frame is the sequence number and the number of frames to make sure frames was not removed
                byte[] frameBytes = new byte[12];
                Buffer.BlockCopy(seqNumBytes, 0, frameBytes, 0, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(plainMessage.FrameCount), 0, frameBytes, 8, 4);

                byte[] cipherSeqNumBytes = EncryptBytes(encryptor, contentType, seqNum, frameIndex, frameBytes);
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

        /// <summary>
        /// Create and return an Initialization Vector (IV) using a given sequence-number and encryptor.
        /// </summary>
        /// <param name="encryptor">the ICryptoTransform to use to do the encryption</param>
        /// <param name="seqNumBytes">a byte-array that is the sequence-number</param>
        /// <returns>a byte-array that comprises the Initialization Vector (IV)</returns>
        private byte[] GenerateIV(ICryptoTransform encryptor, byte[] seqNumBytes)
        {
            // generating an IV by encrypting the sequence number with the random IV and encrypting symmetric key
            byte[] iv = new byte[SecurityParameters.RecordIVLength];
            Buffer.BlockCopy(seqNumBytes, 0, iv, 0, 8);

            byte padding = (byte)((encryptor.OutputBlockSize - (9 % encryptor.OutputBlockSize)) % encryptor.OutputBlockSize);
            for (int i = 8; i < iv.Length; i++)
            {
                iv[i] = padding;
            }

            // Compute the hash value for the region of the input byte-array (iv), starting at index 0,
            // and copy the resulting hash value back into the same byte-array.
            encryptor.TransformBlock(iv, 0, iv.Length, iv, 0);
            return iv;
        }

        /// <summary>
        /// Increment and return the sequence-number.
        /// </summary>
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
                byte[] versionAndType = new[] { (byte)contentType, m_protocolVersion[0], m_protocolVersion[1] };
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
                mac = EmptyArray<byte>.Instance;
            }

            int length = plainBytes.Length + SecurityParameters.MACLength;
            byte padding = 0;

            if (SecurityParameters.BulkCipherAlgorithm != BulkCipherAlgorithm.Null)
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

        /// <summary>
        /// Return a new <see cref="NetMQMessage"/> that contains the decrypted content of the give message.
        /// </summary>
        /// <param name="contentType">This identifies the type of content: ChangeCipherSpec, Handshake, or ApplicationData.</param>
        /// <param name="cipherMessage">the message to decrypt</param>
        /// <returns>a new NetMQMessage with the contents decrypted</returns>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.InvalidFramesCount"/>: Cipher message must have at least 2 frames, iv and sequence number.</exception>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.ReplayAttack"/>: Message already handled or very old message, might be under replay attack.</exception>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.EncryptedFramesMissing"/>: Frames were removed from the encrypted message.</exception>
        public NetMQMessage DecryptMessage(ContentType contentType, NetMQMessage cipherMessage)
        {
            if (SecurityParameters.BulkCipherAlgorithm == BulkCipherAlgorithm.Null &&
              SecurityParameters.MACAlgorithm == MACAlgorithm.Null)
            {
                return cipherMessage;
            }

            if (cipherMessage.FrameCount < 2)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "cipher message should have at least 2 frames, iv and sequence number");
            }

            NetMQFrame ivFrame = cipherMessage.Pop();

            m_decryptionBulkAlgorithm.IV = ivFrame.ToByteArray();

            using (var decryptor = m_decryptionBulkAlgorithm.CreateDecryptor())
            {
                NetMQMessage plainMessage = new NetMQMessage();

                NetMQFrame seqNumFrame = cipherMessage.Pop();

                byte[] frameBytes;
                byte[] seqNumMAC;
                byte[] padding;

                DecryptBytes(decryptor, seqNumFrame.ToByteArray(), out frameBytes, out seqNumMAC, out padding);

                ulong seqNum = BitConverter.ToUInt64(frameBytes, 0);
                int frameCount = BitConverter.ToInt32(frameBytes, 8);

                int frameIndex = 0;

                ValidateBytes(contentType, seqNum, frameIndex, frameBytes, seqNumMAC, padding);

                if (CheckReplayAttack(seqNum))
                {
                    throw new NetMQSecurityException(NetMQSecurityErrorCode.ReplayAttack,
                                  "Message already handled or very old message, might be under replay attack");
                }

                if (frameCount != cipherMessage.FrameCount)
                {
                    throw new NetMQSecurityException(NetMQSecurityErrorCode.EncryptedFramesMissing, "Frames was removed from the encrypted message");
                }

                frameIndex++;

                foreach (NetMQFrame cipherFrame in cipherMessage)
                {
                    byte[] data;
                    byte[] mac;

                    DecryptBytes(decryptor, cipherFrame.ToByteArray(), out data, out mac, out padding);
                    ValidateBytes(contentType, seqNum, frameIndex, data, mac, padding);

                    frameIndex++;

                    plainMessage.Append(data);
                }

                return plainMessage;
            }
        }

        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.EncryptedFrameInvalidLength"/>: The block size must be valid.</exception>
        private void DecryptBytes(ICryptoTransform decryptor, byte[] cipherBytes,
          out byte[] plainBytes, out byte[] mac, out byte[] padding)
        {
            if (cipherBytes.Length % decryptor.InputBlockSize != 0)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.EncryptedFrameInvalidLength, "Invalid block size for cipher bytes");
            }

            byte[] frameBytes = new byte[cipherBytes.Length];

            int dataLength;
            int paddingSize;

            if (SecurityParameters.BulkCipherAlgorithm != BulkCipherAlgorithm.Null)
            {
                decryptor.TransformBlock(cipherBytes, 0, cipherBytes.Length, frameBytes, 0);

                paddingSize = frameBytes[frameBytes.Length - 1] + 1;

                if (paddingSize > decryptor.InputBlockSize)
                {
                    // somebody tamper the message, we don't want throw the exception yet because
                    // of timing issue, we need to throw the exception after the mac check,
                    // therefore we will change the padding size to the size of the block
                    paddingSize = decryptor.InputBlockSize;
                }

                dataLength = frameBytes.Length - paddingSize - SecurityParameters.MACLength;

                // data length can be zero if somebody tamper with the padding
                if (dataLength < 0)
                {
                    dataLength = 0;
                }
            }
            else
            {
                dataLength = frameBytes.Length - SecurityParameters.MACLength;
                frameBytes = cipherBytes;
                paddingSize = 0;
            }

            plainBytes = new byte[dataLength];
            Buffer.BlockCopy(frameBytes, 0, plainBytes, 0, dataLength);

            mac = new byte[SecurityParameters.MACLength];
            Buffer.BlockCopy(frameBytes, dataLength, mac, 0, SecurityParameters.MACLength);

            padding = new byte[paddingSize];
            Buffer.BlockCopy(frameBytes, dataLength + SecurityParameters.MACLength, padding, 0, paddingSize);
        }

        /// <summary>
        /// Check the given arguments and throw a <see cref="NetMQSecurityException"/> if something is amiss.
        /// </summary>
        /// <param name="contentType">This identifies the type of content: ChangeCipherSpec, Handshake, or ApplicationData.</param>
        /// <param name="seqNum"></param>
        /// <param name="frameIndex"></param>
        /// <param name="plainBytes"></param>
        /// <param name="mac"></param>
        /// <param name="padding"></param>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.MACNotMatched"/>: MAC does not match message.</exception>
        public void ValidateBytes(ContentType contentType, ulong seqNum, int frameIndex,
          byte[] plainBytes, byte[] mac, byte[] padding)
        {
            if (SecurityParameters.MACAlgorithm != MACAlgorithm.Null)
            {
                byte[] versionAndType = new[] { (byte)contentType, m_protocolVersion[0], m_protocolVersion[1] };
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
                    throw new NetMQSecurityException(NetMQSecurityErrorCode.MACNotMatched, "MAC does not match message");
                }

                for (int i = 0; i < padding.Length; i++)
                {
                    if (padding[i] != padding.Length - 1)
                    {
                        throw new NetMQSecurityException(NetMQSecurityErrorCode.MACNotMatched, "MAC not matched message");
                    }
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

        /// <summary>
        /// Dispose of all contained resources.
        /// </summary>
        public void Dispose()
        {
            if (m_decryptionBulkAlgorithm != null)
            {
                m_decryptionBulkAlgorithm.Dispose();
                m_decryptionBulkAlgorithm = null;
            }

            if (m_encryptionBulkAlgorithm != null)
            {
                m_encryptionBulkAlgorithm.Dispose();
                m_encryptionBulkAlgorithm = null;
            }

            if (m_decryptionHMAC != null)
            {
                m_decryptionHMAC.Dispose();
                m_decryptionHMAC = null;
            }

            if (m_encryptionHMAC != null)
            {
                m_encryptionHMAC.Dispose();
                m_encryptionHMAC = null;
            }

            if (PRF != null)
            {
                PRF.Dispose();
                PRF = null;
            }
        }
    }
}
