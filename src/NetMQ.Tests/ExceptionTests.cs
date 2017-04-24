#if !NETCOREAPP1_0

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

// ReSharper disable InconsistentNaming

namespace NetMQ.Tests
{
    public class ExceptionTests
    {
        [Fact]
        public void Serialisation()
        {
            RoundTrip(NetMQException.Create("Hello", ErrorCode.BaseErrorNumber));
            RoundTrip(new AddressAlreadyInUseException("Hello"));
            RoundTrip(new EndpointNotFoundException("Hello"));
            RoundTrip(new TerminatingException("Hello"));
            RoundTrip(new InvalidException("Hello"));
            RoundTrip(new FaultException("Hello"));
            RoundTrip(new ProtocolNotSupportedException("Hello"));
            RoundTrip(new HostUnreachableException("Hello"));
            RoundTrip(new FiniteStateMachineException("Hello"));
        }

        [Fact]
        public void Create()
        {
            var before = NetMQException.Create("Hello", ErrorCode.BaseErrorNumber);
            Assert.Equal(ErrorCode.BaseErrorNumber, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void AddressAlreadyInUseException()
        {
            var before = new AddressAlreadyInUseException("Hello");
            Assert.Equal(ErrorCode.AddressAlreadyInUse, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void EndpointNotFoundException()
        {
            var before = new EndpointNotFoundException("Hello");
            Assert.Equal(ErrorCode.EndpointNotFound, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void TerminatingException()
        {
            var before = new TerminatingException("Hello");
            Assert.Equal(ErrorCode.ContextTerminated, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void InvalidException()
        {
            var before = new InvalidException("Hello");
            Assert.Equal(ErrorCode.Invalid, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void FaultException()
        {
            var before = new FaultException("Hello");
            Assert.Equal(ErrorCode.Fault, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void ProtocolNotSupportedException()
        {
            var before = new ProtocolNotSupportedException("Hello");
            Assert.Equal(ErrorCode.ProtocolNotSupported, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void HostUnreachableException()
        {
            var before = new HostUnreachableException("Hello");
            Assert.Equal(ErrorCode.HostUnreachable, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        [Fact]
        public void FiniteStateMachineException()
        {
            var before = new FiniteStateMachineException("Hello");
            Assert.Equal(ErrorCode.FiniteStateMachine, before.ErrorCode);
            Assert.Equal("Hello", before.Message);
        }

        #region Helpers

        private static void RoundTrip(NetMQException before)
        {
            var after = Clone(before);
            Assert.Equal(before.ErrorCode, after.ErrorCode);
            Assert.Equal(before.Message, after.Message);
        }

        private static T Clone<T>(T source)
        {
            return Deserialise<T>(Serialise(source));
        }

        private static Stream Serialise(object source)
        {
            var formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, source);
            return stream;
        }

        private static T Deserialise<T>(Stream stream)
        {
            var formatter = new BinaryFormatter();
            stream.Position = 0;
            return (T)formatter.Deserialize(stream);
        }

        #endregion
    }
}

#endif