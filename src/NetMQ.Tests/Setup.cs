using NUnit.Framework;

namespace NetMQ.Tests
{
    [SetUpFixture]
    public class Setup
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            NetMQConfig.Cleanup(false);
        }
    }
}
