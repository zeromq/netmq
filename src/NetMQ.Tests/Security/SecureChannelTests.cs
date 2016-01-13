using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NetMQ.Security;
using NetMQ.Security.V0_1;
using NUnit.Framework;

namespace NetMQ.Tests.Security
{
    [TestFixture]
    public class SecureChannelTests
    {
        private SecureChannel m_clientSecureChannel;
        private SecureChannel m_serverSecureChannel;

        [SetUp]
        public void Setup()
        {
            X509Certificate2 certificate = new X509Certificate2("NetMQ.Testing.pfx", "1");

            m_serverSecureChannel = new SecureChannel(ConnectionEnd.Server) { Certificate = certificate };

            m_clientSecureChannel = new SecureChannel(ConnectionEnd.Client);
            m_clientSecureChannel.SetVerifyCertificate(c => true);

            IList<NetMQMessage> clientOutgoingMessages = new List<NetMQMessage>();
            IList<NetMQMessage> serverOutgoingMessages = new List<NetMQMessage>();

            bool serverComplete = false;

            bool clientComplete = m_clientSecureChannel.ProcessMessage(null, clientOutgoingMessages);

            while (!serverComplete || !clientComplete)
            {
                if (!serverComplete)
                {
                    foreach (var message in clientOutgoingMessages)
                    {
                        serverComplete = m_serverSecureChannel.ProcessMessage(message, serverOutgoingMessages);

                        if (serverComplete)
                        {
                            break;
                        }
                    }

                    clientOutgoingMessages.Clear();
                }

                if (!clientComplete)
                {
                    foreach (var message in serverOutgoingMessages)
                    {
                        clientComplete = m_clientSecureChannel.ProcessMessage(message, clientOutgoingMessages);

                        if (clientComplete)
                        {
                            break;
                        }
                    }

                    serverOutgoingMessages.Clear();
                }
            }
        }

        [TearDown]
        public void Teardown()
        {
            m_clientSecureChannel.Dispose();
            m_serverSecureChannel.Dispose();
        }


        [Test]
        public void Handshake()
        {
            Assert.IsTrue(m_clientSecureChannel.SecureChannelReady);
            Assert.IsTrue(m_serverSecureChannel.SecureChannelReady);
        }

        [Test]
        public void ClientToServerMessage()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_clientSecureChannel.EncryptApplicationMessage(plainMessage);

            NetMQMessage decryptedMessage = m_serverSecureChannel.DecryptApplicationMessage(cipherMessage);

            Assert.AreEqual(decryptedMessage[0].ConvertToString(), plainMessage[0].ConvertToString());
            Assert.AreEqual(decryptedMessage[0].ConvertToString(), "Hello");
        }

        [Test]
        public void ServerToClient()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            NetMQMessage decryptedMessage = m_clientSecureChannel.DecryptApplicationMessage(cipherMessage);

            Assert.AreEqual(decryptedMessage[0].ConvertToString(), plainMessage[0].ConvertToString());
            Assert.AreEqual(decryptedMessage[0].ConvertToString(), "Hello");
        }

        [Test]
        public void TwoWayMessaging()
        {
            // server to client
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");
            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);
            NetMQMessage decryptedMessage = m_clientSecureChannel.DecryptApplicationMessage(cipherMessage);
            Assert.AreEqual(decryptedMessage[0].ConvertToString(), plainMessage[0].ConvertToString());

            // client to server
            plainMessage = new NetMQMessage();
            plainMessage.Append("Reply");
            cipherMessage = m_clientSecureChannel.EncryptApplicationMessage(plainMessage);
            decryptedMessage = m_serverSecureChannel.DecryptApplicationMessage(cipherMessage);
            Assert.AreEqual(decryptedMessage[0].ConvertToString(), plainMessage[0].ConvertToString());
        }

        [Test]
        public void MultipartMessage()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");
            plainMessage.Append("World");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);
            NetMQMessage decryptedMessage = m_clientSecureChannel.DecryptApplicationMessage(cipherMessage);
            Assert.AreEqual(decryptedMessage[0].ConvertToString(), plainMessage[0].ConvertToString());
            Assert.AreEqual(decryptedMessage[1].ConvertToString(), plainMessage[1].ConvertToString());
            Assert.AreEqual(decryptedMessage[0].ConvertToString(), "Hello");
            Assert.AreEqual(decryptedMessage[1].ConvertToString(), "World");
        }

        [Test]
        public void EmptyMessge()
        {
            NetMQMessage plainMessage = new NetMQMessage();

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);
            NetMQMessage decryptedMessage = m_clientSecureChannel.DecryptApplicationMessage(cipherMessage);

            Assert.AreEqual(decryptedMessage.FrameCount, 0);
        }

        [Test]
        public void WrongProtocolVersion()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            // changing the protocol version
            cipherMessage[0].Buffer[0] = 99;

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessage));

            Assert.AreEqual(exception.ErrorCode, NetMQSecurityErrorCode.InvalidProtocolVersion);
        }

        [Test]
        public void WrongFramesCount()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            // remove the first frame
            cipherMessage.RemoveFrame(cipherMessage.Last);

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessage));

            Assert.AreEqual(NetMQSecurityErrorCode.EncryptedFramesMissing, exception.ErrorCode);
        }

        [Test]
        public void ReorderFrames()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");
            plainMessage.Append("World");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            NetMQFrame lastFrame = cipherMessage.Last;
            cipherMessage.RemoveFrame(lastFrame);

            NetMQFrame oneBeforeLastFrame = cipherMessage.Last;
            cipherMessage.RemoveFrame(oneBeforeLastFrame);

            cipherMessage.Append(lastFrame);
            cipherMessage.Append(oneBeforeLastFrame);

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessage));

            Assert.AreEqual(NetMQSecurityErrorCode.MACNotMatched, exception.ErrorCode);
        }

        [Test]
        public void ChangeEncryptedBytesData()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            cipherMessage.Last.Buffer[0]++;

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessage));

            Assert.AreEqual(NetMQSecurityErrorCode.MACNotMatched, exception.ErrorCode);
        }

        [Test]
        public void ChangeEncryptedFrameLength()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            cipherMessage.RemoveFrame(cipherMessage.Last);

            // appending new frame with length different then block size
            cipherMessage.Append("Hello");

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessage));

            Assert.AreEqual(NetMQSecurityErrorCode.EncryptedFrameInvalidLength, exception.ErrorCode);
        }

        [Test]
        public void ChangeThePadding()
        {
            NetMQMessage plainMessage = new NetMQMessage();
            plainMessage.Append("Hello");

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            cipherMessage.Last.Buffer[15]++;

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessage));

            Assert.AreEqual(NetMQSecurityErrorCode.MACNotMatched, exception.ErrorCode);
        }

        [Test]
        public void ReplayAttach()
        {
            NetMQMessage plainMessage = new NetMQMessage();

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            // make a copy of the message because the method alter the current message
            NetMQMessage cipherMessageCopy = new NetMQMessage(cipherMessage);

            m_clientSecureChannel.DecryptApplicationMessage(cipherMessage);

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessageCopy));

            Assert.AreEqual(NetMQSecurityErrorCode.ReplayAttack, exception.ErrorCode);
        }

        [Test]
        public void DecryptingOldMessage()
        {
            NetMQMessage plainMessage = new NetMQMessage();

            NetMQMessage cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);

            // copy of the first message, we are actually never try to decrypt the first message 
            // (to make sure the exception is because of the old message and not because the message was decrypted twice).
            NetMQMessage cipherMessageCopy = new NetMQMessage(cipherMessage);

            // the window size is 1024, we to decrypt 1024 messages before trying to decrypt the old message
            for (int i = 0; i < 1025; i++)
            {
                plainMessage = new NetMQMessage();

                cipherMessage = m_serverSecureChannel.EncryptApplicationMessage(plainMessage);
                m_clientSecureChannel.DecryptApplicationMessage(cipherMessage);
            }

            NetMQSecurityException exception = Assert.Throws<NetMQSecurityException>(() => m_clientSecureChannel.DecryptApplicationMessage(cipherMessageCopy));

            Assert.AreEqual(NetMQSecurityErrorCode.ReplayAttack, exception.ErrorCode);
        }

        [Test]
        public void DecryptOutOfOrder()
        {
            NetMQMessage plain1 = new NetMQMessage();
            plain1.Append("1");

            NetMQMessage plain2 = new NetMQMessage();
            plain2.Append("2");

            NetMQMessage cipher1 = m_clientSecureChannel.EncryptApplicationMessage(plain1);
            NetMQMessage cipher2 = m_clientSecureChannel.EncryptApplicationMessage(plain2);

            NetMQMessage decrypted2 = m_serverSecureChannel.DecryptApplicationMessage(cipher2);
            NetMQMessage decrypted1 = m_serverSecureChannel.DecryptApplicationMessage(cipher1);

            Assert.AreEqual(decrypted1[0].ConvertToString(), "1");
            Assert.AreEqual(decrypted2[0].ConvertToString(), "2");
        }
    }
}
