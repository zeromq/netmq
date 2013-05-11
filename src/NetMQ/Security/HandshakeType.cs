using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NetMQ.Security
{
  public enum HandshakeType : byte
  {
    HelloRequest = 0,
    ClientHello = 1,
    ServerHello= 2,

    Certificate=11,

    ServerHelloDone=14,

    ClientKetExchange=16,
   
    Finished=20
  }   
}
