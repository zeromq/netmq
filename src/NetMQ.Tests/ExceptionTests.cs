using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;

// ReSharper disable InconsistentNaming

namespace NetMQ.Tests
{
    [TestFixture]
    public class ExceptionTests
    {
        [Test]
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

        [Test]
        public void Create()
        {
            var before = NetMQException.Create("Hello", ErrorCode.BaseErrorNumber);
            Assert.AreEqual(ErrorCode.BaseErrorNumber, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void AddressAlreadyInUseException()
        {
            var before = new AddressAlreadyInUseException("Hello");
            Assert.AreEqual(ErrorCode.AddressAlreadyInUse, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void EndpointNotFoundException()
        {
            var before = new EndpointNotFoundException("Hello");
            Assert.AreEqual(ErrorCode.EndpointNotFound, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void TerminatingException()
        {
            var before = new TerminatingException("Hello");
            Assert.AreEqual(ErrorCode.ContextTerminated, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void InvalidException()
        {
            var before = new InvalidException("Hello");
            Assert.AreEqual(ErrorCode.Invalid, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void FaultException()
        {
            var before = new FaultException("Hello");
            Assert.AreEqual(ErrorCode.Fault, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void ProtocolNotSupportedException()
        {
            var before = new ProtocolNotSupportedException("Hello");
            Assert.AreEqual(ErrorCode.ProtocolNotSupported, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void HostUnreachableException()
        {
            var before = new HostUnreachableException("Hello");
            Assert.AreEqual(ErrorCode.HostUnreachable, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        [Test]
        public void FiniteStateMachineException()
        {
            var before = new FiniteStateMachineException("Hello");
            Assert.AreEqual(ErrorCode.FiniteStateMachine, before.ErrorCode);
            Assert.AreEqual("Hello", before.Message);
        }

        #region Helpers

        private static void RoundTrip(NetMQException before)
        {
            var after = Clone(before);
            Assert.AreEqual(before.ErrorCode, after.ErrorCode);
            Assert.AreEqual(before.Message, after.Message);
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