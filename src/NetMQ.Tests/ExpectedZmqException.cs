
using NUnit.Framework;

namespace NetMQ.Tests
{
    public class ExpectedZmqException : ExpectedExceptionAttribute
    {
        public override bool Match(object obj)
        {
            return base.Match(obj);
        }
    }
}
