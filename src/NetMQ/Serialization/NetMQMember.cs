using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Serialization
{
  public enum EnumSerializationType
  {
    Int32, Int16, Byte, MemberName
  }

  [AttributeUsage(AttributeTargets.Property)]
  public class NetMQMemberAttribute : Attribute
  {
    public NetMQMemberAttribute(int frame)
    {
      Frame = frame;
      Endianness = Endianness.Big;
      EncodingCodePage = Encoding.Unicode.CodePage;
      EnumSerializationType = EnumSerializationType.Int32;
    }

    internal int Frame { get; private set; }

    public Endianness Endianness
    {
      get;
      set;
    }

    public int EncodingCodePage { get; set; }

    public EnumSerializationType EnumSerializationType { get; set; }

    public const int CodePageASCII = 20127;
    public const int CodePageUnicode = 1200;
    public const int CodePageUTF8 = 65001;
    public const int CodePageUTF32 = 12000;

    internal Encoding Encoding
    {
      get { return Encoding.GetEncoding(EncodingCodePage); }
    }
  }
}
