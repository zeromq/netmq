using System;
using System.Text;


using NUnit.Framework;
using FluentAssertions;

using NetMQ;

using MDPCommons;
using TitanicCommons;
using TitanicProtocol;
using TitanicProtocolTests.TestEntities;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicClientTests
    {
        [Test]
        public void ctor_withFakeClient_ShouldReturnvalidObject ()
        {
            IMDPClient fakeMDPClient = new MDPTestClientForTitanicClient ();

            var sut = new TitanicClient (fakeMDPClient);

            sut.Should ().NotBeNull ();
        }

        [Test]
        public void Request_ValidRequestStringString_ShouldReturnExpectedGuid ()
        {
            var expectedId = Guid.NewGuid ();
            var replyMessage = new NetMQMessage ();

            replyMessage.Push (expectedId.ToString ());
            replyMessage.Push (TitanicReturnCode.Ok.ToString ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyMessage = replyMessage, RequestId = expectedId };


            var sut = new TitanicClient (fakeMDPClient);
            var id = sut.Request ("echo", "Hello World!");

            id.Should ().Be (expectedId.ToString ());
        }

        [Test]
        public void Request_ValidRequestStringBytes_ShouldReturnExpectedGuid ()
        {
            var expectedId = Guid.NewGuid ();
            var replyMessage = new NetMQMessage ();

            replyMessage.Push (expectedId.ToString ());
            replyMessage.Push (TitanicReturnCode.Ok.ToString ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyMessage = replyMessage, RequestId = expectedId };


            var sut = new TitanicClient (fakeMDPClient);
            var id = sut.Request ("echo", Encoding.UTF8.GetBytes ("Hello World!"));

            id.Should ().Be (expectedId.ToString ());
        }

        [Test]
        public void Request_ValidRequestStringGeneric_ShouldReturnExpectedGuid ()
        {
            var expectedId = Guid.NewGuid ();
            var replyMessage = new NetMQMessage ();

            replyMessage.Push (expectedId.ToString ());
            replyMessage.Push (TitanicReturnCode.Ok.ToString ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyMessage = replyMessage, RequestId = expectedId };

            var generic = new TestEntity ();
            var sut = new TitanicClient (fakeMDPClient);
            var id = sut.Request ("echo", generic.ConvertToBytes ());

            id.Should ().Be (expectedId.ToString ());
        }

        [Test]
        public void Request_NullOrEmptyServiceName_ShouldThrowArgumentNullException ()
        {
            var fakeMDPClient = new MDPTestClientForTitanicClient ();

            var sut = new TitanicClient (fakeMDPClient);

            Assert.Throws<ArgumentNullException> (() => sut.Request (string.Empty, "Hello World"));
        }

        [Test]
        public void Reply_ExistingRequestStringTimeSpan_ShouldReturnExpectedReply ()
        {
            const string expected_phrase = "Thank God Its Friday";
            var replyFrame = new NetMQFrame (expected_phrase);

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Ok);
            Encoding.UTF8.GetString (reply.Item1).Should ().Be (expected_phrase);
        }

        [Test]
        public void Reply_ExistingRequestStringIntegerInteger_ShouldReturnExpectedReply ()
        {
            const string expected_phrase = "Thank God Its Friday";
            var replyFrame = new NetMQFrame (expected_phrase);

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), 0, TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Ok);
            Encoding.UTF8.GetString (reply.Item1).Should ().Be (expected_phrase);
        }

        [Test]
        public void Reply_ExistingRequestStringTimeSpanGeneric_ShouldReturnExpectedReply ()
        {
            var expected = new TestEntity ();
            var replyFrame = new NetMQFrame (expected.ConvertToBytes ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Ok);
            var result = new TestEntity ();
            result.GenerateFrom (reply.Item1);

            result.Id.Should ().Be (expected.Id);
            result.Name.Should ().Be (expected.Name);
        }

        [Test]
        public void Reply_ExistingRequestStringIntegerTimeSpanGeneric_ShouldReturnExpectedReply ()
        {
            var expected = new TestEntity ();
            var replyFrame = new NetMQFrame (expected.ConvertToBytes ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), 0, TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Ok);
            var result = new TestEntity ();
            result.GenerateFrom (reply.Item1);

            result.Id.Should ().Be (expected.Id);
            result.Name.Should ().Be (expected.Name);
        }

        [Test]
        public void Reply_InvalidReply_ShouldReturnPending ()
        {
            var replyMessage = new NetMQMessage ();
            replyMessage.Push (TitanicReturnCode.Pending.ToString ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyMessage = replyMessage };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Pending);
            reply.Item1.Should ().BeNull ();
        }

        [Test]
        public void Reply_InvalidReply_ShouldReturnUnknown ()
        {
            var replyMessage = new NetMQMessage ();
            replyMessage.Push (TitanicReturnCode.Unknown.ToString ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyMessage = replyMessage };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Unknown);
            reply.Item1.Should ().BeNull ();
        }

        [Test]
        public void Reply_InvalidReply_ShouldReturnFailure ()
        {
            var replyMessage = new NetMQMessage ();
            replyMessage.Push (TitanicReturnCode.Failure.ToString ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyMessage = replyMessage };
            var sut = new TitanicClient (fakeMDPClient);

            var reply = sut.Reply (Guid.NewGuid (), TimeSpan.FromMilliseconds (0));

            reply.Item2.Should ().Be (TitanicReturnCode.Failure);
            reply.Item1.Should ().BeNull ();
        }

        [Test]
        public void GetResult_StringBytes_ShouldReturnExpectedReply ()
        {
            const string expected_phrase = "Thank God Its Friday";
            var replyFrame = new NetMQFrame (expected_phrase);

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var result = sut.GetResult ("echo", Encoding.UTF8.GetBytes (expected_phrase));

            result.Item2.Should ().Be (TitanicReturnCode.Ok);
            result.Item1.Should ().BeSameAs (replyFrame.Buffer);
        }

        [Test]
        public void GetResult_StringString_ShouldReturnExpectedResult ()
        {
            const string expected_phrase = "Thank God Its Friday";
            var replyFrame = new NetMQFrame (expected_phrase);

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var result = sut.GetResult ("echo", expected_phrase);

            result.Item2.Should ().Be (TitanicReturnCode.Ok);
            result.Item1.Should ().BeSameAs (replyFrame.Buffer);
        }

        [Test]
        public void GetResult_StringStringEncoding_ShouldReturnExpectedResult ()
        {
            var enc = Encoding.Unicode;
            const string expected_phrase = "Thank God Its Friday";
            var replyFrame = new NetMQFrame (enc.GetBytes (expected_phrase));

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var result = sut.GetResult ("echo", expected_phrase, enc);

            result.Item2.Should ().Be (TitanicReturnCode.Ok);
            result.Item1.Should ().Be (enc.GetString (replyFrame.Buffer));
        }

        [Test]
        public void GetResult_StringIntegerGeneric_ReturnExpectedResult ()
        {
            var expected = new TestEntity ();
            var replyFrame = new NetMQFrame (expected.ConvertToBytes ());

            var fakeMDPClient = new MDPTestClientForTitanicClient { ReplyDataFrame = replyFrame };
            var sut = new TitanicClient (fakeMDPClient);

            var result = sut.GetResult<TestEntity, TestEntity> ("echo", expected);

            result.Item2.Should ().Be (TitanicReturnCode.Ok);
            result.Item1.Id.Should ().Be (expected.Id);
            result.Item1.Name.Should ().Be (expected.Name);
        }
    }
}
