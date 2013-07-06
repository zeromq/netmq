using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NetMQ.Serialization;
using NetMQ.zmq;

namespace NetMQ.Tests.Serialization
{
  [TestFixture]
  class SerializerBuilderTests
  {
    class TestMessage
    {
      public TestMessage()
      {
        
      }      

      [NetMQMember(0, Endianness = Endianness.Little)]
      public int Number32 { get; set; }

      [NetMQMember(1, Endianness = Endianness.Little)]
      public short Number16 { get; set; }

      [NetMQMember(2, Endianness = Endianness.Little)]
      public long Number64 { get; set; }

      [NetMQMember(3, Endianness = Endianness.Little)]
      public uint NumberU32 { get; set; }

      [NetMQMember(4, Endianness = Endianness.Little)]
      public ulong NumberU64 { get; set; }

      [NetMQMember(5, Endianness = Endianness.Little)]
      public ushort NumberU16 { get; set; }

      [NetMQMember(6, Endianness = Endianness.Little)]
      public double Double { get; set; }

      [NetMQMember(7, Endianness = Endianness.Little)]
      public float Float { get; set; }

      [NetMQMember(8)]
      public string Str { get; set; }

      [NetMQMember(9)]
      public DateTime DateTime { get; set; }

      [NetMQMember(10, EnumSerializationType = EnumSerializationType.MemberName)]
      public ZmqSocketType SocketType { get; set; }

      [NetMQMember(11, EnumSerializationType = EnumSerializationType.Int32)]
      public ZmqSocketType SocketType2 { get; set; }

      [NetMQMember(12)]
      public byte Byte { get; set; }

      [NetMQMember(13)]
      public char Char { get; set; }

      [NetMQMember(14)]
      public bool Boolean { get; set; }
    }

    [Test]
    public void SerializeMessage()
    {      
      SerializerBuilder serializerBuilder = new SerializerBuilder(typeof(TestMessage));

      var serializer = serializerBuilder.CreateSerializer();

      TestMessage message = new TestMessage();
      message.DateTime = new DateTime(2015, 7,15);
      message.Double = 23.00005;
      message.Float = 0.1f;
      message.Number16 = 5;
      message.Number32 = 10000000;
      message.Number64 = int.MaxValue + 100000L;
      message.NumberU16 = short.MaxValue + 1;
      message.NumberU32 = ((uint)int.MaxValue) + 10;
      message.NumberU64 = ((ulong)long.MaxValue) + 10;
      message.SocketType = ZmqSocketType.Dealer;
      message.SocketType2 = ZmqSocketType.Pub;
      message.Str = "Hello world";
      message.Byte = 1;
      message.Char = 'a';
      message.Boolean = true;

      var array = serializer(message);

      Assert.AreEqual(array.Length, 15);

      var deserializer = serializerBuilder.CreateDeserializer();

      var deserializedMessage = (TestMessage)deserializer(array);

      Assert.AreEqual(deserializedMessage.DateTime, message.DateTime);
      Assert.AreEqual(deserializedMessage.Double, message.Double);
      Assert.AreEqual(deserializedMessage.Float, message.Float);
      Assert.AreEqual(deserializedMessage.Number16, message.Number16);
      Assert.AreEqual(deserializedMessage.Number32, message.Number32);
      Assert.AreEqual(deserializedMessage.Number64, message.Number64);
      Assert.AreEqual(deserializedMessage.NumberU16, message.NumberU16);
      Assert.AreEqual(deserializedMessage.NumberU32, message.NumberU32);
      Assert.AreEqual(deserializedMessage.NumberU64, message.NumberU64);
      Assert.AreEqual(deserializedMessage.SocketType, message.SocketType);
      Assert.AreEqual(deserializedMessage.SocketType2, message.SocketType2);
      Assert.AreEqual(deserializedMessage.Str, message.Str);
      Assert.AreEqual(deserializedMessage.Boolean, message.Boolean);
      Assert.AreEqual(deserializedMessage.Char, message.Char);
      Assert.AreEqual(deserializedMessage.Byte, message.Byte);      
    }
         
    class TestMessage2
    {
      [NetMQMember(0)]
      public string Text { get; set; }  
    }

    [Test]
    public void NullString()
    {
      SerializerBuilder serializerBuilder = new SerializerBuilder(typeof(TestMessage2));

      var serializer = serializerBuilder.CreateSerializer();

      var byteArries = serializer(new TestMessage2
        {
          Text = null
        });

      var deserializer = serializerBuilder.CreateDeserializer();

      var message = (TestMessage2)deserializer(byteArries);

      Assert.AreEqual("", message.Text);
    }

    public class TestMessage3
    {
      [NetMQMember(1)]
      public string Text { get; set; }
    }

    [Test, ExpectedException(typeof(ArgumentException))]
    public void MissingFrame()
    {
      SerializerBuilder serializerBuilder = new SerializerBuilder(typeof(TestMessage3));      
    }

    public class TestMessage4
    {
      [NetMQMember(0)]
      public string Text { get; set; }

      [NetMQMember(0)]
      public string Text2 { get; set; }

      [NetMQMember(2)]
      public string Text3 { get; set; }
    }

    [Test, ExpectedException(typeof(ArgumentException))]    
    public void RepeatedFrame()
    {
      SerializerBuilder serializerBuilder = new SerializerBuilder(typeof(TestMessage4));        
    }

    public class TestMessage5
    {
      [NetMQMember(0)]
      public NetMQMessage Text { get; set; }      
    }

    [Test, ExpectedException(typeof(ArgumentException))]
    public void NotPrimitiveType()
    {
      SerializerBuilder serializerBuilder = new SerializerBuilder(typeof(TestMessage5));
    }
  }
}
