using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1
{
  public enum ContentType
  {
    ChangeCipherSpec = 20, 
    //alert(21), 
    Handshake = 22,
    ApplicationData = 23
  }
}
