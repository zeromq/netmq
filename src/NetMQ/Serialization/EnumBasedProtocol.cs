using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Serialization
{
  

  public class EnumBasedProtocol<T> : BaseProtocol<T> where T : struct, IConvertible
  {
    public EnumBasedProtocol(EnumSerializationType enumSerializationType, Endianness endian = Endianness.Big)
    {
      EnumSerializationType = enumSerializationType;
      Endian = endian;
      if (!typeof(T).IsEnum)
      {
        throw new ArgumentException("T must be an enumerated type");
      }
    }

    public EnumSerializationType EnumSerializationType { get; private set; }
    public Endianness Endian { get; private set; }


    protected internal override T ConvertBytesToMessageType(byte[] bytes)
    {
      switch (EnumSerializationType)
      {
        case EnumSerializationType.Int32:
          if (bytes.Length != 4)
          {
            throw new NetMQProtocolException("Wrong message type size");
          }          
          break;
        case EnumSerializationType.Int16:
          if (bytes.Length != 2)
          {
            throw new NetMQProtocolException("Wrong message type size");
          }
          break;
        case EnumSerializationType.Byte:
          if (bytes.Length != 1)
          {
            throw new NetMQProtocolException("Wrong message type size");
          }
          break;
        case EnumSerializationType.MemberName:
          T value;
          
          string text = Convertions.ToString(bytes, Encoding.ASCII);
          bool result = Enum.TryParse(text, out value);                    

          if (!result)
          {
            throw new NetMQProtocolException("Unknown enum value: " + text);
          }
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      return (T)Convertions.ToEnum(typeof(T), bytes, EnumSerializationType, Endian);
    }

    protected internal override byte[] ConvertMessageTypeToBytes(T messageType)
    {
      return Convertions.ToBytes(messageType, EnumSerializationType, Endian);
    }
  }
}
