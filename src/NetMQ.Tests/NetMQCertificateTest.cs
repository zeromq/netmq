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
                var copy  = new NetMQCertificate(key.SecretKeyX85, key.PublicKeyX85);

                Assert.Equal(key.SecretKeyX85, copy.SecretKeyX85);
                Assert.Equal(key.PublicKeyX85, copy.PublicKeyX85);

                Assert.Equal(key.SecretKey, copy.SecretKey);
                Assert.Equal(key.PublicKey, copy.PublicKey);
            }
        }
    }
}
