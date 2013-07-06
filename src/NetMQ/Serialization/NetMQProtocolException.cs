using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Serialization
{
  public class NetMQProtocolException : Exception
  {
    public NetMQProtocolException(string message) : base(message)
    {
    }
  }
}
