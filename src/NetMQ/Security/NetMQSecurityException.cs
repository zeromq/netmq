using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security
{
  public class NetMQSecurityException : Exception
  {
    public NetMQSecurityException(string message) : base(message)
    {
    }

    public NetMQSecurityException()
    {
    }
  }
}
