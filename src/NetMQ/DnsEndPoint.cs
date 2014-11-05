using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;


namespace NetMQ
{
    class DnsEndPoint : EndPoint
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
}
