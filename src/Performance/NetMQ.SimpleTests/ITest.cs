using JetBrains.Annotations;

namespace NetMQ.SimpleTests
{
    internal interface ITest
    {
        [NotNull]
        string TestName { get; }

        void RunTest();
    }
}
