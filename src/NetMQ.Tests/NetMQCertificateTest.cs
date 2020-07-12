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
                var copy  = new NetMQCertificate(key.SecretKeyZ85, key.PublicKeyZ85);

                Assert.Equal(key.SecretKeyZ85, copy.SecretKeyZ85);
                Assert.Equal(key.PublicKeyZ85, copy.PublicKeyZ85);

                Assert.Equal(key.SecretKey, copy.SecretKey);
                Assert.Equal(key.PublicKey, copy.PublicKey);
            }
        }
    }
}
