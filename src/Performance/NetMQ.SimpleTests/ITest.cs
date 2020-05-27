namespace NetMQ.SimpleTests
{
    internal interface ITest
    {
        string? TestName { get; }

        void RunTest();
    }
}
