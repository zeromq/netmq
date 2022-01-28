using System;
using Xunit;

namespace NetMQ.Tests
{
    public class NetMQCertificateTest : IClassFixture<CleanupAfterFixture>
    {
        public NetMQCertificateTest() => NetMQConfig.Cleanup();

        [Fact]
        public void X85()
        {
            for (int i = 0; i < 1000; i++)
            {
                var key = new NetMQCertificate();
                var copy = new NetMQCertificate(key.SecretKeyZ85!, key.PublicKeyZ85);

                Assert.Equal(key.SecretKeyZ85, copy.SecretKeyZ85);
                Assert.Equal(key.PublicKeyZ85, copy.PublicKeyZ85);

                Assert.Equal(key.SecretKey, copy.SecretKey);
                Assert.Equal(key.PublicKey, copy.PublicKey);
            }
        }

        [Fact]
        public void FromPublicKey()
        {
            var key = new NetMQCertificate();
            var copy = NetMQCertificate.FromPublicKey(key.PublicKeyZ85);

            Assert.Null(copy.SecretKeyZ85);
            Assert.Equal(key.PublicKeyZ85, copy.PublicKeyZ85);

            Assert.Null(copy.SecretKey);
            Assert.Equal(key.PublicKey, copy.PublicKey);
        }

        [Fact]
        public void FromSecretKey()
        {
            var key = new NetMQCertificate();
            var copy = new NetMQCertificate().FromSecretKey(key.SecretKeyZ85!);

            Assert.Equal(key.SecretKeyZ85, copy.SecretKeyZ85);
            Assert.Equal(key.PublicKeyZ85, copy.PublicKeyZ85);

            Assert.Equal(key.SecretKey, copy.SecretKey);
            Assert.Equal(key.PublicKey, copy.PublicKey);
        }

        [Fact]
        public void InvalidKeyLength()
        {
            Assert.Throws<ArgumentException>(() => new NetMQCertificate("hkszCq4i}RDe:<!?NIDbGr0%Nl-CP9I^3h>TLT?", "hhRj/hiV-5CE(W!TLjLA%fGuxYmz9g}OZpJt2]cL"));
            Assert.Throws<ArgumentException>(() => new NetMQCertificate("hkszCq4i}RDe:<!?NIDbGr0%Nl-CP9I^3h>TLT?)", "hhRj/hiV-5CE(W!TLjLA%fGuxYmz9g}OZpJt2]c"));
        }

        [Fact]
        public void InvalidKeyCharacter()
        {
            Assert.Throws<ArgumentException>(() => new NetMQCertificate(" kszCq4i}RDe:<!?NIDbGr0%Nl-CP9I^3h>TLT?)", "hhRj/hiV-5CE(W!TLjLA%fGuxYmz9g}OZpJt2]cL"));
        }
    }
}
