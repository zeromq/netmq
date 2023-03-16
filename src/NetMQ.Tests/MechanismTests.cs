using System.Text;
using NetMQ.Core.Mechanisms;
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
