using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Serialization
{
  public class ShortBasedProtocol : BaseProtocol<short>
  {    
    public ShortBasedProtocol(Endianness endian = Endianness.Big)
    {
      Endian = endian;
    }

    public Endianness Endian { get; private set; }

    protected internal override short ConvertBytesToMessageType(byte[] bytes)
    {
      if (bytes.Length != 2)
      {
        throw new NetMQProtocolException("Wrong message type size");
      }


      return Convertions.ToInt16(bytes, Endian);
    }

    protected internal override byte[] ConvertMessageTypeToBytes(short messageType)
    {
      return Convertions.ToBytes(messageType, Endian);
    }
  }
}
