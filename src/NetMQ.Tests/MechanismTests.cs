using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using NetMQ.Core;
using NetMQ.Core.Mechanisms;
using NetMQ.Core.Transports;
using Xunit;

namespace NetMQ.Tests
{
    public class MechanismTests
    {
        private Msg CreateMsg(string data, int lengthDiff)
        {
            Assert.NotNull(data);
            Assert.True(data.Length > 1);
            var length = data.Length + lengthDiff;
            Assert.True(length > 0);
            Assert.True(length < byte.MaxValue - 1);
            
            var msg = new Msg();
            msg.InitGC(new byte[data.Length + 1], 0, data.Length + 1);
            msg.SetFlags(MsgFlags.Command);
            msg.Put((byte)length);
            msg.Put(Encoding.ASCII, data, 1);
            return msg;
        }

        [Fact]
        public void IsCommandShouldReturnTrueForValidCommand()
        {
            var mechanism = new NullMechanism(null!, null!);
            var msg = CreateMsg("READY", 0);
            Assert.True(mechanism.IsCommand("READY", ref msg));
        }

        [Fact]
        public void IsCommandShouldReturnFalseForInvalidCommand()
        {
            var mechanism = new NullMechanism(null!, null!);
            var msg = CreateMsg("READY", -1);
            Assert.False(mechanism.IsCommand("READY", ref msg));
            msg = CreateMsg("READY", 1);
            Assert.False(mechanism.IsCommand("READY", ref msg));
            // this test case would fail due to an exception being throw (in 4.0.1.10 and prior)
            msg = CreateMsg("READY", 2);
            Assert.False(mechanism.IsCommand("READY", ref msg));
        }

        [Fact]
        public void MechanismReadyShouldHandleNullPeerIdentityWhenRecvIdentityIsEnabled()
        {
#pragma warning disable SYSLIB0050
            var streamEngine = (StreamEngine)FormatterServices.GetUninitializedObject(typeof(StreamEngine));
#pragma warning restore SYSLIB0050
            var options = new Options { RecvIdentity = true, HeartbeatInterval = 0 };
            var session = new RecordingSession();
            var mechanism = new NullMechanism(session, options) { PeerIdentity = null };

            SetPrivateField(streamEngine, "m_options", options);
            SetPrivateField(streamEngine, "m_session", session);
            SetPrivateField(streamEngine, "m_mechanism", mechanism);

            var method = typeof(StreamEngine).GetMethod("MechanismReady", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var exception = Record.Exception(() => method!.Invoke(streamEngine, null));
            Assert.Null(exception);
            Assert.Equal(0, session.LastPushedMessageSize);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(instance, value);
        }

        private sealed class RecordingSession : SessionBase
        {
            public int LastPushedMessageSize { get; private set; } = -1;

            public RecordingSession()
                : base(new IOThread(new Ctx(), 0), false, null!, new Options(), null!)
            {
            }

            public override PushMsgResult PushMsg(ref Msg msg)
            {
                LastPushedMessageSize = msg.Size;
                return PushMsgResult.Ok;
            }
        }

        // this test was used to validate the behavior prior to changing the validation logic in Mechanism.IsCommand
        // [Fact]
        // public void IsCommandShouldThrowWhenLengthByteExceedsSize()
        // {
        //     var mechanism = new NullMechanism(null, null);
        //     var msg = CreateMsg("READY", 2);
        //     Assert.Throws<ArgumentOutOfRangeException>(() => mechanism.IsCommand("READY", ref msg));
        // }        
    }
}
