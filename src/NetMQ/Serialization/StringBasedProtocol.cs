using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Serialization
{
  public class StringBasedProtocol : BaseProtocol<string>
  {
    public StringBasedProtocol(Encoding encoding)
    {
      Encoding = encoding;
    }

    public StringBasedProtocol() : this(Encoding.ASCII)
    {
      
    }

    public Encoding Encoding { get; private set; }

    protected internal override string ConvertBytesToMessageType(byte[] bytes)
    {
      return Convertions.ToString(bytes, Encoding);
    }

    protected internal override byte[] ConvertMessageTypeToBytes(string messageType)
    {
      return Convertions.ToBytes(messageType, Encoding);
    }
  }
}
