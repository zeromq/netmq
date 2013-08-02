using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Serialization
{
  public class ByteBasedProtocol : BaseProtocol<byte>
  {   
    protected internal override byte ConvertBytesToMessageType(byte[] bytes)
    {
      if (bytes.Length != 1)
      {
        throw new NetMQProtocolException("Wrong message type size");
      }

      return bytes[0];
    }

    protected internal override byte[] ConvertMessageTypeToBytes(byte messageType)
    {
      return new byte[] {messageType};
    }
  }
}
