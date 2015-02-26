
namespace NetMQ.Tests
{
    public class ExpectedZmqException : NUnit.Framework.ExpectedExceptionAttribute
    {
        public override bool Match(object obj)
        {
            return base.Match(obj);
        }
    }
}
