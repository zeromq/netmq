using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Serialization
{
  public class IntegerBasedProtocol : BaseProtocol<int>
  {
    public IntegerBasedProtocol(Endianness endian = Endianness.Big)
    {
      Endian = endian;
    }

    public Endianness Endian { get; private set; }

    protected internal override int ConvertBytesToMessageType(byte[] bytes)
    {
      if (bytes.Length != 4)
      {
        throw new NetMQProtocolException("Wrong message type size");
      }


      return Convertions.ToInt32(bytes, Endian);
    }

    protected internal override byte[] ConvertMessageTypeToBytes(int messageType)
    {
      return Convertions.ToBytes(messageType, Endian);
    }
  }
}
